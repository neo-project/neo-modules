# RpcSystemAssetTracker for CRON

A plugin for calling contracts and sending assets with raw transactions using UTxO
and without indexing wallets. 
This is NEO's RpcSystemAssetTracker plugin, downgraded for usage with CRONIUM 2.9.4 package 
but extended with cron_send, cron_invoke_contract_as, cron_get_address and cron_tx_block
JSON-RPC methods