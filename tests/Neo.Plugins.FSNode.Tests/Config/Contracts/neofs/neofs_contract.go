package smart_contract

/*
	NeoFS Smart Contract for NEO3.0.

	Utility methods, executed once in deploy stage:
	- Init
	- InitConfig

	User related methods:
	- Deposit
	- Withdraw
	- Bind
	- Unbind

	Inner ring list related methods:
	- InnerRingList
	- InnerRingCandidates
	- IsInnerRing
	- InnerRingCandidateAdd
	- InnerRingCandidateRemove
	- InnerRingUpdate

	Config methods:
	- Config
	- ListConfig
	- SetConfig

	Other utility methods:
	- Version
	- Cheque
*/

import (
	"github.com/nspcc-dev/neo-go/pkg/interop"
	"github.com/nspcc-dev/neo-go/pkg/interop/binary"
	"github.com/nspcc-dev/neo-go/pkg/interop/blockchain"
	"github.com/nspcc-dev/neo-go/pkg/interop/contract"
	"github.com/nspcc-dev/neo-go/pkg/interop/crypto"
	"github.com/nspcc-dev/neo-go/pkg/interop/iterator"
	"github.com/nspcc-dev/neo-go/pkg/interop/runtime"
	"github.com/nspcc-dev/neo-go/pkg/interop/storage"
	"github.com/nspcc-dev/neo-go/pkg/interop/util"
)

type (
	ballot struct {
		id    []byte   // id of the voting decision
		n     [][]byte // already voted inner ring nodes
		block int      // block with the last vote
	}

	node struct {
		pub []byte
	}

	cheque struct {
		id []byte
	}

	record struct {
		key []byte
		val []byte
	}
)

const (
	// native gas token script hash
	tokenHash = "\xfb\xed\xfe\x2e\xd2\x22\x65\x92\xb6\x48\xc4\xda\x97\xb9\xc9\xcd\x5d\xc1\xa6\xa6"

	defaultCandidateFee   = 100 * 1_0000_0000 // 100 Fixed8 Gas
	candidateFeeConfigKey = "InnerRingCandidateFee"

	version = 3

	innerRingKey     = "innerring"
	voteKey          = "ballots"
	candidatesKey    = "candidates"
	cashedChequesKey = "cheques"

	blockDiff        = 20 // change base on performance evaluation
	publicKeySize    = 33
	minInnerRingSize = 3

	maxBalanceAmount = 9000 // Max integer of Fixed12 in JSON bound (2**53-1)

	// hardcoded value to ignore deposit notification in onReceive
	ignoreDepositNotification = "\x57\x0b"
)

var (
	configPrefix = []byte("config")

	ctx storage.Context
)

func init() {
	// The trigger determines whether this smart-contract is being
	// run in 'verification' or 'application' mode.
	if runtime.GetTrigger() != runtime.Application {
		panic("contract has not been called in application node")
	}

	ctx = storage.GetContext()

}

// Init set up initial inner ring node keys.
func Init(args [][]byte) bool {
	if storage.Get(ctx, innerRingKey) != nil {
		panic("neofs: contract already deployed")
	}

	var irList []node

	if len(args) < 3 {
		panic("neofs: at least three inner ring keys must be provided")
	}

	for i := 0; i < len(args); i++ {
		pub := args[i]
		if len(pub) != publicKeySize {
			panic("neofs: incorrect public key length")
		}
		irList = append(irList, node{pub: pub})
	}

	// initialize all storage slices
	setSerialized(ctx, innerRingKey, irList)
	setSerialized(ctx, voteKey, []ballot{})
	setSerialized(ctx, candidatesKey, []node{})
	setSerialized(ctx, cashedChequesKey, []cheque{})

	runtime.Log("neofs: contract initialized")

	return true
}

// InnerRingList returns array of inner ring node keys.
func InnerRingList() []node {
	return getInnerRingNodes(ctx, innerRingKey)
}

// InnerRingCandidates returns array of inner ring candidate node keys.
func InnerRingCandidates() []node {
	return getInnerRingNodes(ctx, candidatesKey)
}

// InnerRingCandidateRemove removes key from the list of inner ring candidates.
func InnerRingCandidateRemove(key []byte) bool {
	if !runtime.CheckWitness(key) {
		panic("irCandidateRemove: you should be the owner of the public key")
	}

	nodes := []node{} // it is explicit declaration of empty slice, not nil
	candidates := getInnerRingNodes(ctx, candidatesKey)

	for i := range candidates {
		c := candidates[i]
		if !bytesEqual(c.pub, key) {
			nodes = append(nodes, c)
		} else {
			runtime.Log("irCandidateRemove: candidate has been removed")
		}
	}

	setSerialized(ctx, candidatesKey, nodes)

	return true
}

// InnerRingCandidateAdd adds key to the list of inner ring candidates.
func InnerRingCandidateAdd(key []byte) bool {
	if !runtime.CheckWitness(key) {
		panic("irCandidateAdd: you should be the owner of the public key")
	}

	c := node{pub: key}
	candidates := getInnerRingNodes(ctx, candidatesKey)

	list, ok := addNode(candidates, c)
	if !ok {
		panic("irCandidateAdd: candidate already in the list")
	}

	from := contract.CreateStandardAccount(key)
	to := runtime.GetExecutingScriptHash()
	fee := getConfig(ctx, candidateFeeConfigKey).(int)

	transferred := contract.Call([]byte(tokenHash),
		"transfer", from, to, fee,
		[]byte(ignoreDepositNotification)).(bool)
	if !transferred {
		panic("irCandidateAdd: failed to transfer funds, aborting")
	}

	runtime.Log("irCandidateAdd: candidate has been added")
	setSerialized(ctx, candidatesKey, list)

	return true
}

// OnPayment is a callback for NEP-17 compatible native GAS contract.
func OnPayment(from interop.Hash160, amount int, data interface{}) {
	rcv := data.(interop.Hash160)
	if bytesEqual(rcv, []byte(ignoreDepositNotification)) {
		return
	}

	caller := runtime.GetCallingScriptHash()
	if !bytesEqual(caller, []byte(tokenHash)) {
		panic("onPayment: only GAS can be accepted for deposit")
	}

	switch len(rcv) {
	case 20:
	case 0:
		rcv = from
	default:
		panic("onPayment: invalid data argument, expected Hash160")
	}

	runtime.Log("onPayment: funds have been transferred")

	tx := runtime.GetScriptContainer()
	runtime.Notify("Deposit", from, amount, rcv, tx.Hash)
}

// Deposit gas assets to this script-hash address in NeoFS balance contract.
func Deposit(from interop.Hash160, amount int, rcv interop.Hash160) bool {
	if !runtime.CheckWitness(from) {
		panic("deposit: you should be the owner of the wallet")
	}

	if amount > maxBalanceAmount {
		panic("deposit: out of max amount limit")
	}

	if amount <= 0 {
		return false
	}
	amount = amount * 100000000

	to := runtime.GetExecutingScriptHash()

	transferred := contract.Call([]byte(tokenHash), "transfer",
		from, to, amount, rcv).(bool)
	if !transferred {
		panic("deposit: failed to transfer funds, aborting")
	}

	return true
}

// Withdraw initialize gas asset withdraw from NeoFS balance.
func Withdraw(user []byte, amount int) bool {
	if !runtime.CheckWitness(user) {
		panic("withdraw: you should be the owner of the wallet")
	}

	if amount < 0 {
		panic("withdraw: non positive amount number")
	}

	if amount > maxBalanceAmount {
		panic("withdraw: out of max amount limit")
	}

	amount = amount * 100000000

	tx := runtime.GetScriptContainer()
	runtime.Notify("Withdraw", user, amount, tx.Hash)

	return true
}

// Cheque sends gas assets back to the user if they were successfully
// locked in NeoFS balance contract.
func Cheque(id, user []byte, amount int, lockAcc []byte) bool {
	irList := getInnerRingNodes(ctx, innerRingKey)
	threshold := len(irList)/3*2 + 1

	cashedCheques := getCashedCheques(ctx)
	hashID := crypto.SHA256(id)

	irKey := innerRingInvoker(irList)
	if len(irKey) == 0 {
		panic("cheque: invoked by non inner ring node")
	}

	c := cheque{id: id}

	list, ok := addCheque(cashedCheques, c)
	if !ok {
		panic("cheque: non unique id")
	}

	n := vote(ctx, hashID, irKey)
	if n >= threshold {
		removeVotes(ctx, hashID)

		from := runtime.GetExecutingScriptHash()

		transferred := contract.Call([]byte(tokenHash),
			"transfer", from, user, amount, nil).(bool)
		if !transferred {
			panic("cheque: failed to transfer funds, aborting")
		}

		runtime.Log("cheque: funds have been transferred")

		setSerialized(ctx, cashedChequesKey, list)
		runtime.Notify("Cheque", id, user, amount, lockAcc)
	}

	return true
}

// Bind public key with user's account to use it in NeoFS requests.
func Bind(user []byte, keys [][]byte) bool {
	if !runtime.CheckWitness(user) {
		panic("binding: you should be the owner of the wallet")
	}

	for i := 0; i < len(keys); i++ {
		pubKey := keys[i]
		if len(pubKey) != publicKeySize {
			panic("binding: incorrect public key size")
		}
	}

	runtime.Notify("Bind", user, keys)

	return true
}

// Unbind public key from user's account
func Unbind(user []byte, keys [][]byte) bool {
	if !runtime.CheckWitness(user) {
		panic("unbinding: you should be the owner of the wallet")
	}

	for i := 0; i < len(keys); i++ {
		pubKey := keys[i]
		if len(pubKey) != publicKeySize {
			panic("unbinding: incorrect public key size")
		}
	}

	runtime.Notify("Unbind", user, keys)

	return true
}

// InnerRingUpdate updates list of inner ring nodes with provided list of
// public keys.
func InnerRingUpdate(chequeID []byte, args [][]byte) bool {
	if len(args) < minInnerRingSize {
		panic("irUpdate: bad arguments")
	}

	irList := getInnerRingNodes(ctx, innerRingKey)
	threshold := len(irList)/3*2 + 1

	irKey := innerRingInvoker(irList)
	if len(irKey) == 0 {
		panic("innerRingUpdate: invoked by non inner ring node")
	}

	c := cheque{id: chequeID}

	cashedCheques := getCashedCheques(ctx)

	chequesList, ok := addCheque(cashedCheques, c)
	if !ok {
		panic("irUpdate: non unique chequeID")
	}

	oldNodes := 0
	candidates := getInnerRingNodes(ctx, candidatesKey)
	newIR := []node{}

loop:
	for i := 0; i < len(args); i++ {
		key := args[i]
		if len(key) != publicKeySize {
			panic("irUpdate: invalid public key in inner ring list")
		}

		// find key in actual inner ring list
		for j := 0; j < len(irList); j++ {
			n := irList[j]
			if bytesEqual(n.pub, key) {
				newIR = append(newIR, n)
				oldNodes++

				continue loop
			}
		}

		// find key in candidates list
		candidates, newIR, ok = rmNodeByKey(candidates, newIR, key)
		if !ok {
			panic("irUpdate: unknown public key in inner ring list")
		}
	}

	if oldNodes < len(newIR)*2/3+1 {
		panic("irUpdate: inner ring change rate must not be more than 1/3 ")
	}

	hashID := crypto.SHA256(chequeID)

	n := vote(ctx, hashID, irKey)
	if n >= threshold {
		removeVotes(ctx, hashID)

		setSerialized(ctx, candidatesKey, candidates)
		setSerialized(ctx, innerRingKey, newIR)
		setSerialized(ctx, cashedChequesKey, chequesList)

		runtime.Notify("InnerRingUpdate", c.id, newIR)
		runtime.Log("irUpdate: inner ring list has been updated")
	}

	return true
}

// IsInnerRing returns 'true' if key is inside of inner ring list.
func IsInnerRing(key []byte) bool {
	if len(key) != publicKeySize {
		panic("isInnerRing: incorrect public key")
	}

	irList := getInnerRingNodes(ctx, innerRingKey)
	for i := range irList {
		node := irList[i]

		if bytesEqual(node.pub, key) {
			return true
		}
	}

	return false
}

// Config returns value of NeoFS configuration with provided key.
func Config(key []byte) interface{} {
	return getConfig(ctx, key)
}

// SetConfig key-value pair as a NeoFS runtime configuration value.
func SetConfig(id, key, val []byte) bool {
	// check if it is inner ring invocation
	irList := getInnerRingNodes(ctx, innerRingKey)
	threshold := len(irList)/3*2 + 1

	irKey := innerRingInvoker(irList)
	if len(irKey) == 0 {
		panic("setConfig: invoked by non inner ring node")
	}

	// check unique id of the operation
	c := cheque{id: id}
	cashedCheques := getCashedCheques(ctx)

	chequesList, ok := addCheque(cashedCheques, c)
	if !ok {
		panic("setConfig: non unique id")
	}

	// vote for new configuration value
	hashID := crypto.SHA256(id)

	n := vote(ctx, hashID, irKey)
	if n >= threshold {
		removeVotes(ctx, hashID)

		setConfig(ctx, key, val)
		setSerialized(ctx, cashedChequesKey, chequesList)

		runtime.Notify("SetConfig", id, key, val)
		runtime.Log("setConfig: configuration has been updated")
	}

	return true
}

// ListConfig returns array of all key-value pairs of NeoFS configuration.
func ListConfig() []record {
	var config []record

	it := storage.Find(ctx, configPrefix)
	for iterator.Next(it) {
		key := iterator.Key(it).([]byte)
		val := iterator.Value(it).([]byte)
		r := record{key: key[len(configPrefix):], val: val}

		config = append(config, r)
	}

	return config
}

// InitConfig set up initial NeoFS key-value configuration.
func InitConfig(args [][]byte) bool {
	if getConfig(ctx, candidateFeeConfigKey) != nil {
		panic("neofs: configuration already installed")
	}

	ln := len(args)
	if ln%2 != 0 {
		panic("initConfig: bad arguments")
	}

	setConfig(ctx, candidateFeeConfigKey, defaultCandidateFee)

	for i := 0; i < ln/2; i++ {
		key := args[i*2]
		val := args[i*2+1]

		setConfig(ctx, key, val)
	}

	runtime.Log("neofs: config has been installed")

	return true
}

// Version of contract.
func Version() int {
	return version
}

// innerRingInvoker returns public key of inner ring node that invoked contract.
func innerRingInvoker(ir []node) []byte {
	for i := 0; i < len(ir); i++ {
		node := ir[i]
		if runtime.CheckWitness(node.pub) {
			return node.pub
		}
	}

	return nil
}

// vote adds ballot for the decision with specific 'id' and returns amount
// on unique voters for that decision.
func vote(ctx storage.Context, id, from []byte) int {
	var (
		newCandidates = []ballot{} // it is explicit declaration of empty slice, not nil
		candidates    = getBallots(ctx)
		found         = -1
		blockHeight   = blockchain.GetHeight()
	)

	for i := 0; i < len(candidates); i++ {
		cnd := candidates[i]
		if bytesEqual(cnd.id, id) {
			voters := cnd.n

			for j := range voters {
				if bytesEqual(voters[j], from) {
					return len(voters)
				}
			}

			voters = append(voters, from)
			cnd = ballot{id: id, n: voters, block: blockHeight}
			found = len(voters)
		}

		// do not add old ballots, they are invalid
		if blockHeight-cnd.block <= blockDiff {
			newCandidates = append(newCandidates, cnd)
		}
	}

	if found < 0 {
		found = 1
		voters := [][]byte{from}

		newCandidates = append(newCandidates, ballot{
			id:    id,
			n:     voters,
			block: blockHeight})
	}

	setSerialized(ctx, voteKey, newCandidates)

	return found
}

// removeVotes clears ballots of the decision that has been accepted by
// inner ring nodes.
func removeVotes(ctx storage.Context, id []byte) {
	var (
		newCandidates = []ballot{} // it is explicit declaration of empty slice, not nil
		candidates    = getBallots(ctx)
	)

	for i := 0; i < len(candidates); i++ {
		cnd := candidates[i]
		if !bytesEqual(cnd.id, id) {
			newCandidates = append(newCandidates, cnd)
		}
	}

	setSerialized(ctx, voteKey, newCandidates)
}

// setSerialized serializes data and puts it into contract storage.
func setSerialized(ctx storage.Context, key interface{}, value interface{}) {
	data := binary.Serialize(value)
	storage.Put(ctx, key, data)
}

// getInnerRingNodes returns deserialized slice of inner ring nodes from storage.
func getInnerRingNodes(ctx storage.Context, key string) []node {
	data := storage.Get(ctx, key)
	if data != nil {
		return binary.Deserialize(data.([]byte)).([]node)
	}

	return []node{}
}

// getInnerRingNodes returns deserialized slice of used cheques.
func getCashedCheques(ctx storage.Context) []cheque {
	data := storage.Get(ctx, cashedChequesKey)
	if data != nil {
		return binary.Deserialize(data.([]byte)).([]cheque)
	}

	return []cheque{}
}

// getInnerRingNodes returns deserialized slice of vote ballots.
func getBallots(ctx storage.Context) []ballot {
	data := storage.Get(ctx, voteKey)
	if data != nil {
		return binary.Deserialize(data.([]byte)).([]ballot)
	}

	return []ballot{}
}

// getConfig returns installed neofs configuration value or nil if it is not set.
func getConfig(ctx storage.Context, key interface{}) interface{} {
	postfix := key.([]byte)
	storageKey := append(configPrefix, postfix...)

	return storage.Get(ctx, storageKey)
}

// setConfig sets neofs configuration value in the contract storage.
func setConfig(ctx storage.Context, key, val interface{}) {
	postfix := key.([]byte)
	storageKey := append(configPrefix, postfix...)

	storage.Put(ctx, storageKey, val)
}

// addCheque returns slice of cheques with appended cheque 'c' and bool flag
// that set to false if cheque 'c' is already presented in the slice 'lst'.
func addCheque(lst []cheque, c cheque) ([]cheque, bool) {
	for i := 0; i < len(lst); i++ {
		if bytesEqual(c.id, lst[i].id) {
			return nil, false
		}
	}

	lst = append(lst, c)

	return lst, true
}

// addNode returns slice of nodes with appended node 'n' and bool flag
// that set to false if node 'n' is already presented in the slice 'lst'.
func addNode(lst []node, n node) ([]node, bool) {
	for i := 0; i < len(lst); i++ {
		if bytesEqual(n.pub, lst[i].pub) {
			return nil, false
		}
	}

	lst = append(lst, n)

	return lst, true
}

// rmNodeByKey returns slice of nodes without node with key 'k',
// slices of nodes 'add' with node with key 'k' and bool flag,
// that set to false if node with a key 'k' does not exists in the slice 'lst'.
func rmNodeByKey(lst, add []node, k []byte) ([]node, []node, bool) {
	var (
		flag   bool
		newLst = []node{} // it is explicit declaration of empty slice, not nil
	)

	for i := 0; i < len(lst); i++ {
		if bytesEqual(k, lst[i].pub) {
			add = append(add, lst[i])
			flag = true
		} else {
			newLst = append(newLst, lst[i])
		}
	}

	return newLst, add, flag
}

// bytesEqual compares two slice of bytes by wrapping them into strings,
// which is necessary with new util.Equal interop behaviour, see neo-go#1176.
func bytesEqual(a []byte, b []byte) bool {
	return util.Equals(string(a), string(b))
}
