// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Network.RPC is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Plugins
{
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

}
