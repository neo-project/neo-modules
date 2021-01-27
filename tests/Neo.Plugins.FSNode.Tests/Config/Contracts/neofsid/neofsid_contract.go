package neofsidcontract

import (
	"github.com/nspcc-dev/neo-go/pkg/interop/binary"
	"github.com/nspcc-dev/neo-go/pkg/interop/blockchain"
	"github.com/nspcc-dev/neo-go/pkg/interop/contract"
	"github.com/nspcc-dev/neo-go/pkg/interop/crypto"
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

	UserInfo struct {
		Keys [][]byte
	}
)

const (
	version   = 1
	blockDiff = 20 // change base on performance evaluation

	voteKey = "ballots"

	netmapContractKey    = "netmapScriptHash"
	containerContractKey = "containerScriptHash"
)

var (
	ctx storage.Context
)

func init() {
	if runtime.GetTrigger() != runtime.Application {
		panic("contract has not been called in application node")
	}

	ctx = storage.GetContext()
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

	runtime.Log("neofsid contract initialized")
}

func AddKey(owner []byte, keys [][]byte) bool {
	var (
		n  int    // number of votes for inner ring invoke
		id []byte // ballot key of the inner ring invocation
	)

	if len(owner) != 25 {
		panic("addKey: incorrect owner")
	}

	netmapContractAddr := storage.Get(ctx, netmapContractKey).([]byte)
	innerRing := contract.Call(netmapContractAddr, "innerRingList").([]irNode)
	threshold := len(innerRing)/3*2 + 1

	irKey := innerRingInvoker(innerRing)
	if len(irKey) == 0 {
		panic("addKey: invocation from non inner ring node")
	}

	info := getUserInfo(ctx, owner)

addLoop:
	for i := 0; i < len(keys); i++ {
		pubKey := keys[i]
		if len(pubKey) != 33 {
			panic("addKey: incorrect public key")
		}

		for j := range info.Keys {
			key := info.Keys[j]
			if bytesEqual(key, pubKey) {
				continue addLoop
			}
		}

		info.Keys = append(info.Keys, pubKey)
	}

	fromKnownContract := fromKnownContract(runtime.GetCallingScriptHash())
	if fromKnownContract {
		n = threshold
		runtime.Log("addKey: processed indirect invoke")
	} else {
		id := invokeIDKeys(owner, keys, []byte("add"))
		n = vote(ctx, id, irKey)
	}

	if n < threshold {
		runtime.Log("addKey: processed invoke from inner ring")
		return true
	}

	removeVotes(ctx, id)
	setSerialized(ctx, owner, info)
	runtime.Log("addKey: key bound to the owner")

	return true
}

func RemoveKey(owner []byte, keys [][]byte) bool {
	if len(owner) != 25 {
		panic("removeKey: incorrect owner")
	}

	netmapContractAddr := storage.Get(ctx, netmapContractKey).([]byte)
	innerRing := contract.Call(netmapContractAddr, "innerRingList").([]irNode)
	threshold := len(innerRing)/3*2 + 1

	irKey := innerRingInvoker(innerRing)
	if len(irKey) == 0 {
		panic("removeKey: invocation from non inner ring node")
	}

	id := invokeIDKeys(owner, keys, []byte("remove"))

	n := vote(ctx, id, irKey)
	if n < threshold {
		runtime.Log("removeKey: processed invoke from inner ring")
		return true
	}

	removeVotes(ctx, id)

	info := getUserInfo(ctx, owner)
	var leftKeys [][]byte

rmLoop:
	for i := range info.Keys {
		key := info.Keys[i]

		for j := 0; j < len(keys); j++ {
			pubKey := keys[j]
			if len(pubKey) != 33 {
				panic("removeKey: incorrect public key")
			}

			if bytesEqual(key, pubKey) {
				continue rmLoop
			}
		}

		leftKeys = append(leftKeys, key)
	}

	info.Keys = leftKeys
	setSerialized(ctx, owner, info)

	return true
}

func Key(owner []byte) [][]byte {
	if len(owner) != 25 {
		panic("key: incorrect owner")
	}

	info := getUserInfo(ctx, owner)

	return info.Keys
}

func Version() int {
	return version
}

func getUserInfo(ctx storage.Context, key interface{}) UserInfo {
	data := storage.Get(ctx, key)
	if data != nil {
		return binary.Deserialize(data.([]byte)).(UserInfo)
	}

	return UserInfo{Keys: [][]byte{}}
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

func invokeID(args []interface{}, prefix []byte) []byte {
	for i := range args {
		arg := args[i].([]byte)
		prefix = append(prefix, arg...)
	}

	return crypto.SHA256(prefix)
}

func invokeIDKeys(owner []byte, keys [][]byte, prefix []byte) []byte {
	prefix = append(prefix, owner...)
	for i := range keys {
		prefix = append(prefix, keys[i]...)
	}

	return crypto.SHA256(prefix)
}

// neo-go#1176
func bytesEqual(a []byte, b []byte) bool {
	return util.Equals(string(a), string(b))
}

func fromKnownContract(caller []byte) bool {
	containerContractAddr := storage.Get(ctx, containerContractKey).([]byte)
	if bytesEqual(caller, containerContractAddr) {
		return true
	}

	return false
}
