// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Plugins.RestServer.Exceptions
{
    internal static class RestErrorCodes
    {
        //=========================Rest Codes=========================
        public const int GenericException = 1000;
        public const int ParameterFormatException = 1001;
        public const int RequestBodyInvalid = 1002;
        public const int ScriptHashFormat = 1003;
        //=========================Node Codes=========================
        public const int NodeException = 2000;
        //=========================Wallet Codes=======================
        public const int WalletException = 3000;
        public const int WalletAmountInvalid = 3001;
        public const int WalletInsufficientFunds = 3002;
        public const int WalletSessionNotFound = 3003;
        public const int WalletReachedMaximumFee = 3004;
        public const int WalletAddressInvalid = 3005;
        public const int WalletDeleteAccountFailed = 3006;
        public const int WalletSessionException = 3007;
        public const int WalletFileNotFound = 3008;
        public const int WalletOpenException = 3009;
    }
}
