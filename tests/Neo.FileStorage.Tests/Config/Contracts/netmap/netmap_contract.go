package netmapcontract

import (
	"github.com/nspcc-dev/neo-go/pkg/interop/binary"
	"github.com/nspcc-dev/neo-go/pkg/interop/blockchain"
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

	storageNode struct {
		info []byte
	}

	ballot struct {
		id    []byte   // id of the voting decision
		n     [][]byte // already voted inner ring nodes
		block int      // block with the last vote
	}

	netmapNode struct {
		node  storageNode
		state nodeState
	}

	nodeState int

	record struct {
		key []byte
		val []byte
	}
)

const (
	version   = 1
	blockDiff = 20 // change base on performance evaluation

	voteKey       = "ballots"
	netmapKey     = "netmap"
	innerRingKey  = "innerring"
	configuredKey = "initconfig"

	snapshot0Key  = "snapshotCurrent"
	snapshot1Key  = "snapshotPrevious"
	snapshotEpoch = "snapshotEpoch"
)

const (
	_ nodeState = iota
	onlineState
	offlineState
)

var (
	configPrefix = []byte("config")

	ctx storage.Context
)

func init() {
	if runtime.GetTrigger() != runtime.Application {
		panic("contract has not been called in application node")
	}

	ctx = storage.GetContext()
}

// Init function sets up initial list of inner ring public keys and should
// be invoked once at neofs infrastructure setup.
func Init(keys [][]byte) {
	if storage.Get(ctx, innerRingKey) != nil {
		panic("netmap: contract already initialized")
	}

	var irList []irNode

	for i := 0; i < len(keys); i++ {
		key := keys[i]
		irList = append(irList, irNode{key: key})
	}

	setSerialized(ctx, innerRingKey, irList)

	// epoch number is a little endian int, it doesn't need to be serialized
	storage.Put(ctx, snapshotEpoch, 0)

	// simplified: this used for const sysfee in AddPeer method
	setSerialized(ctx, netmapKey, []netmapNode{})
	setSerialized(ctx, snapshot0Key, []netmapNode{})
	setSerialized(ctx, snapshot1Key, []netmapNode{})
	setSerialized(ctx, voteKey, []ballot{})

	runtime.Log("netmap contract initialized")
}

func InnerRingList() []irNode {
	return getIRNodes(ctx)
}

func UpdateInnerRing(keys [][]byte) bool {
	innerRing := getIRNodes(ctx)
	threshold := len(innerRing)/3*2 + 1

	irKey := innerRingInvoker(innerRing)
	if len(irKey) == 0 {
		panic("updateInnerRing: this method must be invoked by inner ring nodes")
	}

	var irList []irNode

	for i := 0; i < len(keys); i++ {
		key := keys[i]
		irList = append(irList, irNode{key: key})
	}

	rawIRList := binary.Serialize(irList)
	hashIRList := crypto.SHA256(rawIRList)

	n := vote(ctx, hashIRList, irKey)
	if n >= threshold {
		runtime.Log("updateInnerRing: inner ring list updated")
		setSerialized(ctx, innerRingKey, irList)
		removeVotes(ctx, hashIRList)
	} else {
		runtime.Log("updateInnerRing: processed invoke from inner ring")
	}

	return true
}

func AddPeer(nodeInfo []byte) bool {
	innerRing := getIRNodes(ctx)
	threshold := len(innerRing)/3*2 + 1

	irKey := innerRingInvoker(innerRing)
	if len(irKey) == 0 {
		publicKey := nodeInfo[2:35] // offset:2, len:33
		if !runtime.CheckWitness(publicKey) {
			panic("addPeer: witness check failed")
		}
		runtime.Notify("AddPeer", nodeInfo)
		return true
	}

	candidate := storageNode{
		info: nodeInfo,
	}
	rawCandidate := binary.Serialize(candidate)
	hashCandidate := crypto.SHA256(rawCandidate)

	nm := addToNetmap(ctx, candidate)

	n := vote(ctx, hashCandidate, irKey)
	if n >= threshold {
		if nm == nil {
			runtime.Log("addPeer: storage node already in the netmap")
		} else {
			setSerialized(ctx, netmapKey, nm)
			runtime.Log("addPeer: add storage node to the network map")
		}
		removeVotes(ctx, hashCandidate)
	} else {
		runtime.Log("addPeer: processed invoke from inner ring")
	}

	return true
}

func UpdateState(state int, publicKey []byte) bool {
	if len(publicKey) != 33 {
		panic("updateState: incorrect public key")
	}

	innerRing := getIRNodes(ctx)
	threshold := len(innerRing)/3*2 + 1

	irKey := innerRingInvoker(innerRing)
	if len(irKey) == 0 {
		if !runtime.CheckWitness(publicKey) {
			panic("updateState: witness check failed")
		}
		runtime.Notify("UpdateState", state, publicKey)
		return true
	}

	switch nodeState(state) {
	case offlineState:
		newNetmap := removeFromNetmap(ctx, publicKey)

		hashID := invokeID([]interface{}{publicKey}, []byte("delete"))
		n := vote(ctx, hashID, irKey)
		if n >= threshold {
			runtime.Log("updateState: remove storage node from the network map")
			setSerialized(ctx, netmapKey, newNetmap)
			removeVotes(ctx, hashID)
		} else {
			runtime.Log("updateState: processed invoke from inner ring")
		}
	default:
		panic("updateState: unsupported state")
	}

	return true
}

func NewEpoch(epochNum int) bool {
	innerRing := getIRNodes(ctx)
	threshold := len(innerRing)/3*2 + 1

	irKey := innerRingInvoker(innerRing)
	if len(irKey) == 0 {
		panic("newEpoch: this method must be invoked by inner ring nodes")
	}

	currentEpoch := storage.Get(ctx, snapshotEpoch).(int)
	if epochNum <= currentEpoch {
		return false // ignore invocations with invalid epoch
	}

	data0snapshot := getSnapshot(ctx, snapshot0Key)
	dataOnlineState := filterNetmap(ctx, onlineState)

	hashID := invokeID([]interface{}{epochNum}, []byte("epoch"))

	n := vote(ctx, hashID, irKey)
	if n >= threshold {
		runtime.Log("newEpoch: process new epoch")

		// todo: check if provided epoch number is bigger than current
		storage.Put(ctx, snapshotEpoch, epochNum)

		// put actual snapshot into previous snapshot
		setSerialized(ctx, snapshot1Key, data0snapshot)

		// put netmap into actual snapshot
		setSerialized(ctx, snapshot0Key, dataOnlineState)

		removeVotes(ctx, hashID)
		runtime.Notify("NewEpoch", epochNum)
	} else {
		runtime.Log("newEpoch: processed invoke from inner ring")
	}

	return true
}

func Epoch() int {
	return storage.Get(ctx, snapshotEpoch).(int)
}

func Netmap() []storageNode {
	return getSnapshot(ctx, snapshot0Key)
}

func Snapshot(diff int) []storageNode {
	var key string
	switch diff {
	case 0:
		key = snapshot0Key
	case 1:
		key = snapshot1Key
	default:
		panic("snapshot: incorrect diff")
	}

	return getSnapshot(ctx, key)
}

func SnapshotByEpoch(epoch int) []storageNode {
	currentEpoch := storage.Get(ctx, snapshotEpoch).(int)

	return Snapshot(currentEpoch - epoch)
}

func Config(key []byte) interface{} {
	return getConfig(ctx, key)
}

func SetConfig(id, key, val []byte) bool {
	// check if it is inner ring invocation
	innerRing := getIRNodes(ctx)
	threshold := len(innerRing)/3*2 + 1

	irKey := innerRingInvoker(innerRing)
	if len(irKey) == 0 {
		panic("setConfig: invoked by non inner ring node")
	}

	// check unique id of the operation
	hashID := invokeID([]interface{}{id, key, val}, []byte("config"))
	n := vote(ctx, hashID, irKey)

	if n >= threshold {
		removeVotes(ctx, hashID)
		setConfig(ctx, key, val)

		runtime.Log("setConfig: configuration has been updated")
	}

	return true
}

func InitConfig(args [][]byte) bool {
	if storage.Get(ctx, configuredKey) != nil {
		panic("netmap: configuration already installed")
	}

	ln := len(args)
	if ln%2 != 0 {
		panic("initConfig: bad arguments")
	}

	for i := 0; i < ln/2; i++ {
		key := args[i*2]
		val := args[i*2+1]

		setConfig(ctx, key, val)
	}

	storage.Put(ctx, configuredKey, true)
	runtime.Log("netmap: config has been installed")

	return true
}

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

func Version() int {
	return version
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

func addToNetmap(ctx storage.Context, n storageNode) []netmapNode {
	var (
		newNode    = n.info
		newNodeKey = newNode[2:35]

		netmap = getNetmapNodes(ctx)
		node   = netmapNode{
			node:  n,
			state: onlineState,
		}
	)

	for i := range netmap {
		netmapNode := netmap[i].node.info
		netmapNodeKey := netmapNode[2:35]

		if bytesEqual(newNodeKey, netmapNodeKey) {
			return nil
		}
	}

	netmap = append(netmap, node)

	return netmap
}

func removeFromNetmap(ctx storage.Context, key []byte) []netmapNode {
	var (
		netmap    = getNetmapNodes(ctx)
		newNetmap = []netmapNode{}
	)

	for i := 0; i < len(netmap); i++ {
		item := netmap[i]
		node := item.node.info
		publicKey := node[2:35] // offset:2, len:33

		if !bytesEqual(publicKey, key) {
			newNetmap = append(newNetmap, item)
		}
	}

	return newNetmap
}

func filterNetmap(ctx storage.Context, st nodeState) []storageNode {
	var (
		netmap = getNetmapNodes(ctx)
		result = []storageNode{}
	)

	for i := 0; i < len(netmap); i++ {
		item := netmap[i]
		if item.state == st {
			result = append(result, item.node)
		}
	}

	return result
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

func getIRNodes(ctx storage.Context) []irNode {
	data := storage.Get(ctx, innerRingKey)
	if data != nil {
		return binary.Deserialize(data.([]byte)).([]irNode)
	}

	return []irNode{}
}

func getNetmapNodes(ctx storage.Context) []netmapNode {
	data := storage.Get(ctx, netmapKey)
	if data != nil {
		return binary.Deserialize(data.([]byte)).([]netmapNode)
	}

	return []netmapNode{}
}

func getSnapshot(ctx storage.Context, key string) []storageNode {
	data := storage.Get(ctx, key)
	if data != nil {
		return binary.Deserialize(data.([]byte)).([]storageNode)
	}

	return []storageNode{}
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

func getConfig(ctx storage.Context, key interface{}) interface{} {
	postfix := key.([]byte)
	storageKey := append(configPrefix, postfix...)

	return storage.Get(ctx, storageKey)
}

func setConfig(ctx storage.Context, key, val interface{}) {
	postfix := key.([]byte)
	storageKey := append(configPrefix, postfix...)

	storage.Put(ctx, storageKey, val)
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
