# FileStoragePlugin

Enable `neo-cli` serve as a NeoFS storage node. For implement detail please refer to [Docs](./Docs/index.md).

In order to run a NeoFS storage node, you should run `neo-cli` in morph chain with this plugin.

## Prerequisites

1. You should have a wallet and [deposit](#How to deposit) your gas in NeoFS contract in Neo blockchain.

## Installation

1. Configure your `neo-cli` with NeoFS side chain parameters. So that your `neo-cli` can join NeoFS network.

2. Start `neo-cli`, install this plugin.
  ```neo> install FileStorageST```

3. Stop `neo-cli`  and configure your storage service by editing `Plugins/FileStorageST/config.json`

4. Sync your node to latest block.

5. Open your wallet from [Prerequisites](#prerequisites) which has deposited `gas` in Neo blockchain.

6. You can use `fs balance` to  check the gas balance you deposited.


7. Start storage service.

   ```neo> fs start storage```
## How to deposit

  Send gas to NeoFS contract address from your wallet account.
  NeoFS contract address:

  * mainnet: [NNxVrKjLsRkWsmGgmuNXLcMswtxTGaNQLk](https://neo3.neotube.io/contract/0x2cafa46838e8b564468ebd868dcafdd99dce6221)
  * testnet: [Nb8jADHaYuH2e46koNEfTSrKj7iEPEEY7p](https://neo3.testnet.neotube.io/contract/0x51cf687eb6625eb1a2b98b0fb4e9d52bdf95f3a6)

> The gas you deposit is managed in BalanceContract. It has nothing to do with the gas in morph chain.
> Gas in morph chain is only to use for sending transaction. Once your storage node is online, InnerRing nodes will send morph chain gas to your node every epoch.

### Commands

* ```fs start storage```

  Start storage service.

* ```fs balance```

  Show your deposited gas balance.

* ```fs config```

  Show network parameters configured in contract.

* ```fs epoch```

  Show current epoch.


* ```fs node info```

  Show local node info.

* ```fs node state```

  Show node state, `online` or `offline`.

* ```fs node map```

  List all nodes in node map.


* ```fs container nodes [ContainerID|string]```

  List nodes that chosed by the container

* ```fs container info```

  Show container
