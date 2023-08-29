// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Network.RPC is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Collections.Generic;
namespace Neo.Plugins;
public static class RpcErrorCode
{

    public const int InternalServerError = -32603;
    public const int BadRequest = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;

    public const int UnknownBlock = -101;
    public const int UnknownContract = -102;
    public const int UnknownTransaction = -103;
    public const int UnknownStorageItem = -104;
    public const int UnknownScriptContainer = -105;
    public const int UnknownStateRoot = -106;
    public const int UnknownSession = -107;
    public const int UnknownIterator = -108;
    public const int UnknownHeight = -109;

    public const int InsufficientFundsWallet = -300;
    public const int WalletFeeLimit = -301;
    public const int NoOpenedWallet = -302;
    public const int WalletNotFound = -303;
    public const int WalletNotSupported = -304;

    public const int AccessDenied = -400;

    public const int VerificationFailed = -500;
    public const int AlreadyExists = -501;
    public const int MempoolCapReached = -502;
    public const int AlreadyInPool = -503;
    public const int InsufficientNetworkFee = -504;
    public const int PolicyFailed = -505;
    public const int InvalidScript = -506;
    public const int InvalidAttribute = -507;
    public const int InvalidSignature = -508;
    public const int InvalidSize = -509;
    public const int ExpiredTransaction = -510;
    public const int InsufficientFunds = -511;
    public const int InvalidVerificationFunction = -512;

    public const int SessionsDisabled = -601;
    public const int OracleDisabled = -602;
    public const int OracleRequestFinished = -603;
    public const int OracleRequestNotFound = -604;
    public const int OracleNotDesignatedNode = -605;
    public const int UnsupportedState = -606;
    public const int InvalidProof = -607;
    public const int ExecutionFailed = -608;

}
