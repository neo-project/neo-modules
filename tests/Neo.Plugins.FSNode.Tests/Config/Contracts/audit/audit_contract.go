package auditcontract

import (
	"github.com/nspcc-dev/neo-go/pkg/interop"
	"github.com/nspcc-dev/neo-go/pkg/interop/contract"
	"github.com/nspcc-dev/neo-go/pkg/interop/crypto"
	"github.com/nspcc-dev/neo-go/pkg/interop/iterator"
	"github.com/nspcc-dev/neo-go/pkg/interop/runtime"
	"github.com/nspcc-dev/neo-go/pkg/interop/storage"
	"github.com/nspcc-dev/neo-go/pkg/interop/util"
)

type (
	irNode struct {
		key interop.PublicKey
	}

	auditHeader struct {
		epoch int
		cid   []byte
		from  interop.PublicKey
	}
)

// Audit key is a combination of epoch, container ID and public key of node that
// executed audit. Together it should be no more than 64 bytes. We can't shrink
// epoch and container ID since we iterate over these values. But we can shrink
// public key by using first bytes of the hashed value.

const maxKeySize = 24 // 24 + 32 (container ID length) + 8 (epoch length) = 64

func (a auditHeader) ID() []byte {
	var buf interface{} = a.epoch

	hashedKey := crypto.SHA256(a.from)
	shortedKey := hashedKey[:maxKeySize]

	return append(buf.([]byte), append(a.cid, shortedKey...)...)
}

const (
	version = 1

	netmapContractKey   = "netmapScriptHash"
	netmapContractKeyLn = len(netmapContractKey)
)

var ctx storage.Context

func init() {
	if runtime.GetTrigger() != runtime.Application {
		panic("contract has not been called in application node")
	}

	ctx = storage.GetContext()
}

func Init(addrNetmap interop.Hash160) {
	if storage.Get(ctx, netmapContractKey) != nil {
		panic("init: contract already deployed")
	}

	if len(addrNetmap) != 20 {
		panic("init: incorrect length of contract script hash")
	}

	storage.Put(ctx, netmapContractKey, addrNetmap)

	runtime.Log("audit contract initialized")
}

func Put(rawAuditResult []byte) bool {
	netmapContractAddr := storage.Get(ctx, netmapContractKey).([]byte)
	innerRing := contract.Call(netmapContractAddr, "innerRingList").([]irNode)

	hdr := newAuditHeader(rawAuditResult)
	presented := false

	for i := range innerRing {
		ir := innerRing[i]
		if bytesEqual(ir.key, hdr.from) {
			presented = true

			break
		}
	}

	if !runtime.CheckWitness(hdr.from) || !presented {
		panic("audit: put access denied")
	}

	storage.Put(ctx, hdr.ID(), rawAuditResult)

	runtime.Log("audit: result has been saved")

	return true
}

func Get(id []byte) []byte {
	return storage.Get(ctx, id).([]byte)
}

func List() [][]byte {
	it := storage.Find(ctx, []byte{})

	return list(it)
}

func ListByEpoch(epoch int) [][]byte {
	it := storage.Find(ctx, epoch)

	return list(it)
}

func ListByCID(epoch int, cid []byte) [][]byte {
	var buf interface{} = epoch

	prefix := append(buf.([]byte), cid...)
	it := storage.Find(ctx, prefix)

	return list(it)
}

func ListByNode(epoch int, cid []byte, key interop.PublicKey) [][]byte {
	hdr := auditHeader{
		epoch: epoch,
		cid:   cid,
		from:  key,
	}

	it := storage.Find(ctx, hdr.ID())

	return list(it)
}

func list(it iterator.Iterator) [][]byte {
	var result [][]byte

	for iterator.Next(it) {
		key := iterator.Key(it).([]byte)
		if len(key) == netmapContractKeyLn {
			continue
		}

		result = append(result, key)
	}

	return result
}

func Version() int {
	return version
}

// readNext reads length from first byte and then reads data (max 127 bytes).
func readNext(input []byte) ([]byte, int) {
	var buf interface{} = input[0]
	ln := buf.(int)

	return input[1 : 1+ln], 1 + ln
}

func newAuditHeader(input []byte) auditHeader {
	offset := int(input[1])
	offset = 2 + offset + 1 // version prefix + version len + epoch prefix

	var buf interface{} = input[offset : offset+8] // [ 8 integer bytes ]
	epoch := buf.(int)

	offset = offset + 8

	// cid is a nested structure with raw bytes
	// [ cid struct prefix (wireType + len = 2 bytes), cid value wireType (1 byte), ... ]
	cid, cidOffset := readNext(input[offset+2+1:])

	// key is a raw byte
	// [ public key wireType (1 byte), ... ]
	key, _ := readNext(input[offset+2+1+cidOffset+1:])

	return auditHeader{
		epoch,
		cid,
		key,
	}
}

// neo-go#1176
func bytesEqual(a []byte, b []byte) bool {
	return util.Equals(string(a), string(b))
}
