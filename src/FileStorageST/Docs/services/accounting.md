# Accounting Service

Handle balance request and return balance of owner in NeoFS network. Specific request and response definition refer to [service.proto](https://github.com/neo-ngd/neofs-api-csharp/blob/master/src/Neo.FileStorage.API/accounting/service.proto)

Users' balances are managed in [BalanceContract](https://github.com/nspcc-dev/neofs-contract/blob/master/balance/balance_contract.go). Storage node get balance by invoking [balanceOf](https://github.com/nspcc-dev/neofs-contract/blob/63673a5e54f5716e47ede8d51969c17dccac51c1/balance/balance_contract.go#L121) via [MorphInvoker](../../../FileStorageBase/Invoker/Morph/MorphInvoker.cs)
