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

public class RpcError
{
    public int Code { get; set; }
    public string Message { get; set; }
    public string Data { get; set; }
    public RpcError(int code, string message, string data = "")
    {
        Code = code;
        Message = message;
        Data = data;
    }

    public static RpcError ParseError(string data) => new RpcError(RpcErrorCode.BadRequest, "Parse RpcError", data);

    // Missing helper methods
    public static RpcError InvalidRequestError(string data) => new RpcError(RpcErrorCode.InvalidRequest, "Invalid request", data);

    public static RpcError MethodNotFoundError(string data) => new RpcError(RpcErrorCode.MethodNotFound, "Method not found", data);

    public static RpcError InvalidParamsError(string data) => new RpcError(RpcErrorCode.InvalidParams, "Invalid params", data);

    public static RpcError InternalServerError(string data) => new RpcError(RpcErrorCode.InternalServerError, "Internal RpcError", data);

    public static RpcError ErrorWithCode(int code, string message) => new RpcError(code, message);

    // Helper to wrap an existing RpcError with data
    public static RpcError WrapErrorWithData(RpcError error, string data) => new RpcError(error.Code, error.Message, data);


    public override string ToString()
    {
        if (string.IsNullOrEmpty(Data))
        {
            return $"{Message} ({Code})";
        }
        return $"{Message} ({Code}) - {Data}";
    }

    public string ErrorMessage => string.IsNullOrEmpty(Data) ? $"{Message}" : $"{Message} - {Data}";

}

public static class RpcErrorFactory
{
    private static readonly Dictionary<int, string> DefaultMessages = new Dictionary<int, string> {

      {RpcErrorCode.InternalServerError, "Internal server RpcError"},
      {RpcErrorCode.BadRequest, "Bad request"},
      {RpcErrorCode.InvalidRequest, "Invalid request"},
      {RpcErrorCode.MethodNotFound, "Method not found"},
      {RpcErrorCode.InvalidParams, "Invalid params"},

      {RpcErrorCode.UnknownBlock, "Unknown block"},
      {RpcErrorCode.UnknownContract, "Unknown contract"},
      {RpcErrorCode.UnknownTransaction, "Unknown transaction"},
      {RpcErrorCode.UnknownStorageItem, "Unknown storage item"},
      {RpcErrorCode.UnknownScriptContainer, "Unknown script container"},
      {RpcErrorCode.UnknownStateRoot, "Unknown state root"},
      {RpcErrorCode.UnknownSession, "Unknown session"},
      {RpcErrorCode.UnknownIterator, "Unknown iterator"},
      {RpcErrorCode.UnknownHeight, "Unknown height"},

      {RpcErrorCode.InsufficientFundsWallet, "Insufficient funds in wallet"},
      {RpcErrorCode.WalletFeeLimit, "Wallet fee limit exceeded"},
      {RpcErrorCode.NoOpenedWallet, "No opened wallet"},
      {RpcErrorCode.WalletNotFound, "Wallet not found"},
      {RpcErrorCode.WalletNotSupported, "Wallet not supported"},

      { RpcErrorCode.AccessDenied, "Access denied"},

      {RpcErrorCode.VerificationFailed, "Inventory verification failed"},
      {RpcErrorCode.AlreadyExists, "Inventory already exists"},
      {RpcErrorCode.MempoolCapReached, "Memory pool capacity reached"},
      {RpcErrorCode.AlreadyInPool, "Already in transaction pool"},
      {RpcErrorCode.InsufficientNetworkFee, "Insufficient network fee"},
      {RpcErrorCode.PolicyFailed, "Policy check failed"},
      {RpcErrorCode.InvalidScript, "Invalid transaction script"},
      {RpcErrorCode.InvalidAttribute, "Invalid transaction attribute"},
      {RpcErrorCode.InvalidSignature, "Invalid transaction signature"},
      {RpcErrorCode.InvalidSize, "Invalid inventory size"},
      {RpcErrorCode.ExpiredTransaction, "Expired transaction"},
      {RpcErrorCode.InsufficientFunds, "Insufficient funds for fee"},
      {RpcErrorCode.InvalidVerificationFunction, "Invalid contract verification"},

      {RpcErrorCode.SessionsDisabled, "State iterator sessions disabled"},
      {RpcErrorCode.OracleDisabled, "Oracle service disabled"},
      {RpcErrorCode.OracleRequestFinished, "Oracle request already finished"},
      {RpcErrorCode.OracleRequestNotFound, "Oracle request not found"},
      {RpcErrorCode.OracleNotDesignatedNode, "Not a designated oracle node"},
      {RpcErrorCode.UnsupportedState, "Old state not supported"},
      {RpcErrorCode.InvalidProof, "Invalid state proof"},
      {RpcErrorCode.ExecutionFailed, "Contract execution failed"}

    };

    public static RpcError NewError(int code, string message = null, string data = "")
    {
        message ??= DefaultMessages[code];
        return new RpcError(code, message, data);
    }

    public static RpcError NewCustomError(int code, string message)
    {
        return new RpcError(code, message, null);
    }

    public static bool Contains(int code)
    {
        return DefaultMessages.ContainsKey(code);
    }

    public static readonly RpcError ErrInvalidParams = NewError(RpcErrorCode.InvalidParams);

    public static readonly RpcError ErrUnknownBlock = NewError(RpcErrorCode.UnknownBlock);

    public static readonly RpcError ErrUnknownContract = NewError(RpcErrorCode.UnknownContract);
}
