package containercontract

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

	extendedACL struct {
		val []byte
		sig []byte
		pub interop.PublicKey
	}
)

const (
	version   = 1
	voteKey   = "ballots"
	ownersKey = "ownersList"
	blockDiff = 20 // change base on performance evaluation

	neofsIDContractKey = "identityScriptHash"
	balanceContractKey = "balanceScriptHash"
	netmapContractKey  = "netmapScriptHash"
	containerFeeKey    = "ContainerFee"

	containerIDSize = 32 // SHA256 size
)

var (
	containerFeeTransferMsg = []byte("container creation fee")
	eACLPrefix              = []byte("eACL")

	ctx storage.Context
)

func init() {
	if runtime.GetTrigger() != runtime.Application {
		panic("contract has not been called in application node")
	}

	ctx = storage.GetContext()
}

func Init(addrNetmap, addrBalance, addrID []byte) {
	if storage.Get(ctx, netmapContractKey) != nil &&
		storage.Get(ctx, balanceContractKey) != nil &&
		storage.Get(ctx, neofsIDContractKey) != nil {
		panic("init: contract already deployed")
	}

	if len(addrNetmap) != 20 || len(addrBalance) != 20 || len(addrID) != 20 {
		panic("init: incorrect length of contract script hash")
	}

	storage.Put(ctx, netmapContractKey, addrNetmap)
	storage.Put(ctx, balanceContractKey, addrBalance)
	storage.Put(ctx, neofsIDContractKey, addrID)

	runtime.Log("container contract initialized")
}

func Put(container, signature, publicKey []byte) bool {
	netmapContractAddr := storage.Get(ctx, netmapContractKey).([]byte)
	innerRing := contract.Call(netmapContractAddr, "innerRingList").([]irNode)
	threshold := len(innerRing)/3*2 + 1

	offset := int(container[1])
	offset = 2 + offset + 4                  // version prefix + version size + owner prefix
	ownerID := container[offset : offset+25] // offset + size of owner
	containerID := crypto.SHA256(container)
	neofsIDContractAddr := storage.Get(ctx, neofsIDContractKey).([]byte)

	// If invoked from storage node, ignore it.
	// Inner ring will find tx, validate it and send it again.
	irKey := innerRingInvoker(innerRing)
	if len(irKey) == 0 {
		// check provided key
		if !isSignedByOwnerKey(container, signature, ownerID, publicKey) {
			// check keys from NeoFSID
			keys := contract.Call(neofsIDContractAddr, "key", ownerID).([][]byte)
			if !verifySignature(container, signature, keys) {
				panic("put: invalid owner signature")
			}
		}

		runtime.Notify("containerPut", container, signature, publicKey)

		return true
	}

	from := walletToScripHash(ownerID)
	balanceContractAddr := storage.Get(ctx, balanceContractKey).([]byte)
	containerFee := contract.Call(netmapContractAddr, "config", containerFeeKey).(int)
	hashCandidate := invokeID([]interface{}{container, signature, publicKey}, []byte("put"))

	n := vote(ctx, hashCandidate, irKey)
	if n >= threshold {
		removeVotes(ctx, hashCandidate)
		// todo: check if new container with unique container id

		for i := 0; i < len(innerRing); i++ {
			node := innerRing[i]
			to := contract.CreateStandardAccount(node.key)

			tx := contract.Call(balanceContractAddr, "transferX",
				from,
				to,
				containerFee,
				containerFeeTransferMsg, // consider add container id to the message
			)
			if !tx.(bool) {
				// todo: consider using `return false` to remove votes
				panic("put: can't transfer assets for container creation")
			}
		}

		addContainer(ctx, containerID, ownerID, container)
		// try to remove underscore at v0.92.0
		_ = contract.Call(neofsIDContractAddr, "addKey", ownerID, [][]byte{publicKey})

		runtime.Log("put: added new container")
	} else {
		runtime.Log("put: processed invoke from inner ring")
	}

	return true
}

func Delete(containerID, signature []byte) bool {
	netmapContractAddr := storage.Get(ctx, netmapContractKey).([]byte)
	innerRing := contract.Call(netmapContractAddr, "innerRingList").([]irNode)
	threshold := len(innerRing)/3*2 + 1

	ownerID := getOwnerByID(ctx, containerID)
	if len(ownerID) == 0 {
		panic("delete: container does not exist")
	}

	// If invoked from storage node, ignore it.
	// Inner ring will find tx, validate it and send it again.
	irKey := innerRingInvoker(innerRing)
	if len(irKey) == 0 {
		// check provided key
		neofsIDContractAddr := storage.Get(ctx, neofsIDContractKey).([]byte)
		keys := contract.Call(neofsIDContractAddr, "key", ownerID).([][]byte)

		if !verifySignature(containerID, signature, keys) {
			panic("delete: invalid owner signature")
		}

		runtime.Notify("containerDelete", containerID, signature)
		return true
	}

	hashCandidate := invokeID([]interface{}{containerID, signature}, []byte("delete"))

	n := vote(ctx, hashCandidate, irKey)
	if n >= threshold {
		removeVotes(ctx, hashCandidate)
		removeContainer(ctx, containerID, ownerID)
		runtime.Log("delete: remove container")
	} else {
		runtime.Log("delete: processed invoke from inner ring")
	}

	return true
}

func Get(containerID []byte) []byte {
	return storage.Get(ctx, containerID).([]byte)
}

func Owner(containerID []byte) []byte {
	return getOwnerByID(ctx, containerID)
}

func List(owner []byte) [][]byte {
	if len(owner) == 0 {
		return getAllContainers(ctx)
	}

	var list [][]byte

	owners := getList(ctx, ownersKey)
	for i := 0; i < len(owners); i++ {
		ownerID := owners[i]
		if len(owner) != 0 && !bytesEqual(owner, ownerID) {
			continue
		}

		containers := getList(ctx, ownerID)

		for j := 0; j < len(containers); j++ {
			container := containers[j]
			list = append(list, container)
		}
	}

	return list
}

func SetEACL(eACL, signature []byte) bool {
	// get container ID
	offset := int(eACL[1])
	offset = 2 + offset + 4
	containerID := eACL[offset : offset+32]

	ownerID := getOwnerByID(ctx, containerID)
	if len(ownerID) == 0 {
		panic("setEACL: container does not exists")
	}

	neofsIDContractAddr := storage.Get(ctx, neofsIDContractKey).([]byte)
	keys := contract.Call(neofsIDContractAddr, "key", ownerID).([][]byte)

	if !verifySignature(eACL, signature, keys) {
		panic("setEACL: invalid eACL signature")
	}

	rule := extendedACL{
		val: eACL,
		sig: signature,
	}

	key := append(eACLPrefix, containerID...)
	setSerialized(ctx, key, rule)

	runtime.Log("setEACL: success")

	return true
}

func EACL(containerID []byte) extendedACL {
	ownerID := getOwnerByID(ctx, containerID)
	if len(ownerID) == 0 {
		panic("getEACL: container does not exists")
	}

	eacl := getEACL(ctx, containerID)

	if len(eacl.sig) == 0 {
		return eacl
	}

	// attach corresponding public key if it was not revoked from neofs id

	neofsIDContractAddr := storage.Get(ctx, neofsIDContractKey).([]byte)
	keys := contract.Call(neofsIDContractAddr, "key", ownerID).([][]byte)

	for i := range keys {
		key := keys[i]
		if crypto.ECDsaSecp256r1Verify(eacl.val, key, eacl.sig) {
			eacl.pub = key

			break
		}
	}

	return eacl
}

func Version() int {
	return version
}

func addContainer(ctx storage.Context, id []byte, owner []byte, container []byte) {
	addOrAppend(ctx, ownersKey, owner)
	addOrAppend(ctx, owner, id)
	storage.Put(ctx, id, container)
}

func removeContainer(ctx storage.Context, id []byte, owner []byte) {
	n := remove(ctx, owner, id)

	// if it was last container, remove owner from the list of owners
	if n == 0 {
		_ = remove(ctx, ownersKey, owner)
	}

	storage.Delete(ctx, id)
}

func addOrAppend(ctx storage.Context, key interface{}, value []byte) {
	list := getList(ctx, key)
	for i := 0; i < len(list); i++ {
		if bytesEqual(list[i], value) {
			return
		}
	}

	if len(list) == 0 {
		list = [][]byte{value}
	} else {
		list = append(list, value)
	}

	setSerialized(ctx, key, list)
}

// remove returns amount of left elements in the list
func remove(ctx storage.Context, key interface{}, value []byte) int {
	var (
		list    = getList(ctx, key)
		newList = [][]byte{}
	)

	for i := 0; i < len(list); i++ {
		if !bytesEqual(list[i], value) {
			newList = append(newList, list[i])
		}
	}

	ln := len(newList)
	if ln == 0 {
		storage.Delete(ctx, key)
	} else {
		setSerialized(ctx, key, newList)
	}

	return ln
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

func getList(ctx storage.Context, key interface{}) [][]byte {
	data := storage.Get(ctx, key)
	if data != nil {
		return binary.Deserialize(data.([]byte)).([][]byte)
	}

	return [][]byte{}
}

func getAllContainers(ctx storage.Context) [][]byte {
	var list [][]byte

	it := storage.Find(ctx, []byte{})
	for iterator.Next(it) {
		key := iterator.Key(it).([]byte)
		if len(key) == containerIDSize {
			list = append(list, key)
		}
	}

	return list
}

func getBallots(ctx storage.Context) []ballot {
	data := storage.Get(ctx, voteKey)
	if data != nil {
		return binary.Deserialize(data.([]byte)).([]ballot)
	}

	return []ballot{}
}

func getEACL(ctx storage.Context, cid []byte) extendedACL {
	key := append(eACLPrefix, cid...)
	data := storage.Get(ctx, key)
	if data != nil {
		return binary.Deserialize(data.([]byte)).(extendedACL)
	}

	return extendedACL{val: []byte{}, sig: []byte{}, pub: []byte{}}
}

func setSerialized(ctx storage.Context, key, value interface{}) {
	data := binary.Serialize(value)
	storage.Put(ctx, key, data)
}

func walletToScripHash(wallet []byte) []byte {
	return wallet[1 : len(wallet)-4]
}

func verifySignature(msg, sig []byte, keys [][]byte) bool {
	for i := range keys {
		key := keys[i]
		if crypto.ECDsaSecp256r1Verify(msg, key, sig) {
			return true
		}
	}

	return false
}

func invokeID(args []interface{}, prefix []byte) []byte {
	for i := range args {
		arg := args[i].([]byte)
		prefix = append(prefix, arg...)
	}

	return crypto.SHA256(prefix)
}

func getOwnerByID(ctx storage.Context, id []byte) []byte {
	owners := getList(ctx, ownersKey)
	for i := 0; i < len(owners); i++ {
		ownerID := owners[i]
		containers := getList(ctx, ownerID)

		for j := 0; j < len(containers); j++ {
			container := containers[j]
			if bytesEqual(container, id) {
				return ownerID
			}
		}
	}

	return nil
}

// neo-go#1176
func bytesEqual(a []byte, b []byte) bool {
	return util.Equals(string(a), string(b))
}

func isSignedByOwnerKey(msg, sig, owner, key []byte) bool {
	if !isOwnerFromKey(owner, key) {
		return false
	}

	return crypto.ECDsaSecp256r1Verify(msg, key, sig)
}

func isOwnerFromKey(owner []byte, key []byte) bool {
	ownerSH := walletToScripHash(owner)
	keySH := contract.CreateStandardAccount(key)

	return bytesEqual(ownerSH, keySH)
}
