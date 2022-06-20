# NetmapService

#### LocalNodeInfo

Return node info. When storage node start, it will invoke [`addPeer`](https://github.com/nspcc-dev/neofs-contract/blob/63673a5e54f5716e47ede8d51969c17dccac51c1/netmap/netmap_contract.go#L197) every two epoch as heart beat for InnerRing node to detect node online.

> Storage node use LOCODE to indicate location in attribute. InnterRing node will parse LOCODE of storage node to human-readable  values and store in NetmapContract.

#### NetworkInfo

Return current epoch and network magic number.

