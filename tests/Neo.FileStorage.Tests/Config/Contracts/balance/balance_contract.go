package balancecontract

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
	irNode struct {
		key []byte
	}

	ballot struct {
		id    []byte   // id of the voting decision
		n     [][]byte // already voted inner ring nodes
		block int      // block with the last vote
	}

	// Token holds all token info.
	Token struct {
		// Ticker symbol
		Symbol string
		// Amount of decimals
		Decimals int
		// Storage key for circulation value
		CirculationKey string
	}

	Account struct {
		// Active  balance
		Balance int
		// Until valid for lock accounts
		Until int
		// Parent field used in lock accounts, used to return assets back if
		// account wasn't burnt.
		Parent []byte
	}
)

const (
	symbol      = "NEOFS"
	decimals    = 12
	circulation = "MainnetGAS"
	version     = 1

	voteKey   = "ballots"
	blockDiff = 20 // change base on performance evaluation

	netmapContractKey    = "netmapScriptHash"
	containerContractKey = "containerScriptHash"
)

var (
	lockTransferMsg   = []byte("lock assets to withdraw")
	unlockTransferMsg = []byte("asset lock expired")

	ctx   storage.Context
	token Token
)

// CreateToken initializes the Token Interface for the Smart Contract to operate with.
func CreateToken() Token {
	return Token{
		Symbol:         symbol,
		Decimals:       decimals,
		CirculationKey: circulation,
	}
}

func init() {
	if runtime.GetTrigger() != runtime.Application {
		panic("contract has not been called in application node")
	}

	ctx = storage.GetContext()
	token = CreateToken()
}

func Init(addrNetmap, addrContainer []byte) {
	if storage.Get(ctx, netmapContractKey) != nil {
		panic("init: contract already deployed")
	}

	if len(addrNetmap) != 20 || len(addrContainer) != 20 {
		panic("init: incorrect length of contract script hash")
	}

	storage.Put(ctx, netmapContractKey, addrNetmap)
	storage.Put(ctx, containerContractKey, addrContainer)

	runtime.Log("balance contract initialized")
}

func Symbol() string {
	return token.Symbol
}

func Decimals() int {
	return token.Decimals
}

func TotalSupply() int {
	return token.getSupply(ctx)
}

func BalanceOf(account interop.Hash160) int {
	return token.balanceOf(ctx, account)
}

func Transfer(from, to interop.Hash160, amount int, data interface{}) bool {
	return token.transfer(ctx, from, to, amount, false, nil)
}

func TransferX(from, to interop.Hash160, amount int, details []byte) bool {
	var (
		n        int    // number of votes for inner ring invoke
		hashTxID []byte // ballot key of the inner ring invocation
	)

	netmapContractAddr := storage.Get(ctx, netmapContractKey).([]byte)
	innerRing := contract.Call(netmapContractAddr, "innerRingList").([]irNode)
	threshold := len(innerRing)/3*2 + 1

	irKey := innerRingInvoker(innerRing)
	if len(irKey) == 0 {
		panic("transferX: this method must be invoked from inner ring")
	}

	fromKnownContract := fromKnownContract(runtime.GetCallingScriptHash())
	if fromKnownContract {
		n = threshold
		runtime.Log("transferX: processed indirect invoke")
	} else {
		hashTxID = invokeID([]interface{}{from, to, amount}, []byte("transfer"))
		n = vote(ctx, hashTxID, irKey)
	}

	if n >= threshold {
		removeVotes(ctx, hashTxID)

		result := token.transfer(ctx, from, to, amount, true, details)
		if result {
			runtime.Log("transferX: success")
		} else {
			// consider panic there
			runtime.Log("transferX: fail")
		}

		return result
	}

	return false
}

func Lock(txID []byte, from, to interop.Hash160, amount, until int) bool {
	netmapContractAddr := storage.Get(ctx, netmapContractKey).([]byte)
	innerRing := contract.Call(netmapContractAddr, "innerRingList").([]irNode)
	threshold := len(innerRing)/3*2 + 1

	irKey := innerRingInvoker(innerRing)
	if len(irKey) == 0 {
		panic("lock: this method must be invoked from inner ring")
	}

	hashTxID := invokeID([]interface{}{txID, from, to, amount, until}, []byte("lock"))

	n := vote(ctx, hashTxID, irKey)
	if n >= threshold {
		removeVotes(ctx, hashTxID)

		lockAccount := Account{
			Balance: 0,
			Until:   until,
			Parent:  from,
		}
		setSerialized(ctx, to, lockAccount)

		result := token.transfer(ctx, from, to, amount, true, lockTransferMsg)
		if !result {
			// consider using `return false` to remove votes
			panic("lock: can't lock funds")
		}

		runtime.Log("lock: created lock account")
		runtime.Notify("Lock", txID, from, to, amount, until)
	}

	runtime.Log("lock: processed invoke from inner ring")

	return true
}

func NewEpoch(epochNum int) bool {
	netmapContractAddr := storage.Get(ctx, netmapContractKey).([]byte)
	innerRing := contract.Call(netmapContractAddr, "innerRingList").([]irNode)
	threshold := len(innerRing)/3*2 + 1

	irKey := innerRingInvoker(innerRing)
	if len(irKey) == 0 {
		panic("epochNum: this method must be invoked from inner ring")
	}

	epochID := invokeID([]interface{}{epochNum}, []byte("epoch"))

	n := vote(ctx, epochID, irKey)
	if n >= threshold {
		removeVotes(ctx, epochID)
		it := storage.Find(ctx, []byte{})
		for iterator.Next(it) {
			addr := iterator.Key(it).([]byte)
			if len(addr) != 20 {
				continue
			}

			acc := getAccount(ctx, addr)
			if acc.Until == 0 {
				continue
			}

			if epochNum >= acc.Until {
				// return assets back to the parent
				token.transfer(ctx, addr, acc.Parent, acc.Balance, true, unlockTransferMsg)
			}
		}
	}

	runtime.Log("newEpoch: processed invoke from inner ring")

	return true
}

func Mint(to interop.Hash160, amount int, details []byte) bool {
	netmapContractAddr := storage.Get(ctx, netmapContractKey).([]byte)
	innerRing := contract.Call(netmapContractAddr, "innerRingList").([]irNode)
	threshold := len(innerRing)/3*2 + 1

	irKey := innerRingInvoker(innerRing)
	if len(irKey) == 0 {
		panic("burn: this method must be invoked from inner ring")
	}

	mintID := invokeID([]interface{}{to, amount, details}, []byte("mint"))

	n := vote(ctx, mintID, irKey)
	if n >= threshold {
		removeVotes(ctx, mintID)

		ok := token.transfer(ctx, nil, to, amount, true, details)
		if !ok {
			panic("mint: can't transfer assets")
		}

		supply := token.getSupply(ctx)
		supply = supply + amount
		storage.Put(ctx, token.CirculationKey, supply)
		runtime.Log("mint: assets were minted")
		runtime.Notify("Mint", to, amount)
	}

	return true
}

func Burn(from interop.Hash160, amount int, details []byte) bool {
	netmapContractAddr := storage.Get(ctx, netmapContractKey).([]byte)
	innerRing := contract.Call(netmapContractAddr, "innerRingList").([]irNode)
	threshold := len(innerRing)/3*2 + 1

	irKey := innerRingInvoker(innerRing)
	if len(irKey) == 0 {
		panic("burn: this method must be invoked from inner ring")
	}

	burnID := invokeID([]interface{}{from, amount, details}, []byte("burn"))

	n := vote(ctx, burnID, irKey)
	if n >= threshold {
		removeVotes(ctx, burnID)

		ok := token.transfer(ctx, from, nil, amount, true, details)
		if !ok {
			panic("burn: can't transfer assets")
		}

		supply := token.getSupply(ctx)
		if supply < amount {
			panic("panic, negative supply after burn")
		}

		supply = supply - amount
		storage.Put(ctx, token.CirculationKey, supply)
		runtime.Log("burn: assets were burned")
		runtime.Notify("Burn", from, amount)
	}

	return true
}

func Version() int {
	return version
}

// getSupply gets the token totalSupply value from VM storage.
func (t Token) getSupply(ctx storage.Context) int {
	supply := storage.Get(ctx, t.CirculationKey)
	if supply != nil {
		return supply.(int)
	}

	return 0
}

// BalanceOf gets the token balance of a specific address.
func (t Token) balanceOf(ctx storage.Context, holder interop.Hash160) int {
	acc := getAccount(ctx, holder)

	return acc.Balance
}

func (t Token) transfer(ctx storage.Context, from, to interop.Hash160, amount int, innerRing bool, details []byte) bool {
	amountFrom, ok := t.canTransfer(ctx, from, to, amount, innerRing)
	if !ok {
		return false
	}

	if len(from) == 20 {
		if amountFrom.Balance == amount {
			storage.Delete(ctx, from)
		} else {
			amountFrom.Balance = amountFrom.Balance - amount // neo-go#953
			setSerialized(ctx, from, amountFrom)
		}
	}

	if len(to) == 20 {
		amountTo := getAccount(ctx, to)
		amountTo.Balance = amountTo.Balance + amount // neo-go#953
		setSerialized(ctx, to, amountTo)
	}

	runtime.Notify("Transfer", from, to, amount)
	runtime.Notify("TransferX", from, to, amount, details)

	return true
}

// canTransfer returns the amount it can transfer.
func (t Token) canTransfer(ctx storage.Context, from, to interop.Hash160, amount int, innerRing bool) (Account, bool) {
	var (
		emptyAcc = Account{}
	)

	if !innerRing {
		if len(to) != 20 || !isUsableAddress(from) {
			runtime.Log("transfer: bad script hashes")
			return emptyAcc, false
		}
	} else if len(from) == 0 {
		return emptyAcc, true
	}

	amountFrom := getAccount(ctx, from)
	if amountFrom.Balance < amount {
		runtime.Log("transfer: not enough assets")
		return emptyAcc, false
	}

	// return amountFrom value back to transfer, reduces extra Get
	return amountFrom, true
}

// isUsableAddress checks if the sender is either the correct NEO address or SC address.
func isUsableAddress(addr interop.Hash160) bool {
	if len(addr) == 20 {
		if runtime.CheckWitness(addr) {
			return true
		}

		// Check if a smart contract is calling script hash
		callingScriptHash := runtime.GetCallingScriptHash()
		if bytesEqual(callingScriptHash, addr) {
			return true
		}
	}

	return false
}

func innerRingInvoker(ir []irNode) []byte {
	for i := 0; i < len(ir); i++ {
		node := ir[i]
		if runtime.CheckWitness(node.key) {
			return node.key
		}
	}

	return nil
}

func vote(ctx storage.Context, id, from []byte) int {
	var (
		newCandidates []ballot
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
		voters := [][]byte{from}
		newCandidates = append(newCandidates, ballot{
			id:    id,
			n:     voters,
			block: blockHeight})
		found = 1
	}

	setSerialized(ctx, voteKey, newCandidates)

	return found
}

func removeVotes(ctx storage.Context, id []byte) {
	var (
		newCandidates []ballot
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

func getBallots(ctx storage.Context) []ballot {
	data := storage.Get(ctx, voteKey)
	if data != nil {
		return binary.Deserialize(data.([]byte)).([]ballot)
	}

	return []ballot{}
}

func setSerialized(ctx storage.Context, key interface{}, value interface{}) {
	data := binary.Serialize(value)
	storage.Put(ctx, key, data)
}

func getAccount(ctx storage.Context, key interface{}) Account {
	data := storage.Get(ctx, key)
	if data != nil {
		return binary.Deserialize(data.([]byte)).(Account)
	}

	return Account{}
}

func invokeID(args []interface{}, prefix []byte) []byte {
	for i := range args {
		arg := args[i].([]byte)
		prefix = append(prefix, arg...)
	}

	return crypto.SHA256(prefix)
}

// neo-go#1176
func bytesEqual(a []byte, b []byte) bool {
	return util.Equals(string(a), string(b))
}

/*
   Check if invocation made from known container or audit contracts.
   This is necessary because calls from these contracts require to do transfer
   without signature collection (1 invoke transfer).

   IR1, IR2, IR3, IR4 -(4 invokes)-> [ Container Contract ] -(1 invoke)-> [ Balance Contract ]

   We can do 1 invoke transfer if:
   - invoke happened from inner ring node,
   - it is indirect invocation from other smart-contract.

   However there is a possible attack, when malicious inner ring node creates
   malicious smart-contract in morph chain to do inderect call.

   MaliciousIR  -(1 invoke)-> [ Malicious Contract ] -(1 invoke) -> [ Balance Contract ]

   To prevent that, we have to allow 1 invoke transfer from authorised well known
   smart-contracts, that will be set up at `Init` method.
*/

func fromKnownContract(caller interop.Hash160) bool {
	containerContractAddr := storage.Get(ctx, containerContractKey).([]byte)

	return bytesEqual(caller, containerContractAddr)
}
