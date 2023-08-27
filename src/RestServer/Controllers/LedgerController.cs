// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.AspNetCore.Mvc;
using Neo.SmartContract.Native;
using Microsoft.AspNetCore.Http;
using Neo.Plugins.RestServer.Extensions;
using Neo.Plugins.RestServer.Exceptions;
using System.Net.Mime;
using Neo.Network.P2P.Payloads;
using Neo.Plugins.RestServer.Models.Error;
using Neo.Plugins.RestServer.Models.Blockchain;
using Neo.Plugins.RestServer.Models.Ledger;

namespace Neo.Plugins.RestServer.Controllers
{
    [Route("/api/v1/ledger")]
    [Produces(MediaTypeNames.Application.Json)]
    [Consumes(MediaTypeNames.Application.Json)]
    [ApiExplorerSettings(GroupName = "v1")]
    [ApiController]
    public class LedgerController : ControllerBase
    {
        private readonly NeoSystem _neosystem;
        private readonly RestServerSettings _settings;

        public LedgerController()
        {
            _neosystem = RestServerPlugin.NeoSystem ?? throw new NodeNetworkException();
            _settings = RestServerSettings.Current;
        }

        #region Accounts

        /// <summary>
        /// Gets all the accounts that hold gas on the blockchain.
        /// </summary>
        /// <returns>An array of account details object.</returns>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpGet("gas/accounts", Name = "GetGasAccounts")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AccountDetails[]))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
        public IActionResult ShowGasAccounts()
        {
            var accounts = NativeContract.GAS.ListAccounts(_neosystem.StoreView, _neosystem.Settings);
            return Ok(accounts.OrderByDescending(o => o.Balance));
        }

        /// <summary>
        /// Get all the accounts that hold neo on the blockchain.
        /// </summary>
        /// <returns>An array of account details object.</returns>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpGet("neo/accounts", Name = "GetNeoAccounts")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AccountDetails[]))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
        public IActionResult ShowNeoAccounts()
        {
            var accounts = NativeContract.NEO.ListAccounts(_neosystem.StoreView, _neosystem.Settings);
            return Ok(accounts.OrderByDescending(o => o.Balance));
        }

        #endregion

        #region Blocks

        /// <summary>
        /// Get blocks from the blockchain.
        /// </summary>
        /// <param name="skip" example="1">Page</param>
        /// <param name="take" example="50">Page Size</param>
        /// <returns>An array of Block Header Objects</returns>
        /// <response code="204">No more pages.</response>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpGet("blocks", Name = "GetBlocks")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Header[]))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
        public IActionResult GetBlocks(
            [FromQuery(Name = "page")]
            uint skip = 1,
            [FromQuery(Name = "size")]
            uint take = 1)
        {
            if (skip < 1 || take < 1 || take > _settings.MaxPageSize)
                throw new InvalidParameterRangeException();
            //var start = (skip - 1) * take + startIndex;
            //var end = start + take;
            var start = NativeContract.Ledger.CurrentIndex(_neosystem.StoreView) - ((skip - 1) * take);
            var end = start - take;
            var lstOfBlocks = new List<Header>();
            for (uint i = start; i > end; i--)
            {
                var block = NativeContract.Ledger.GetBlock(_neosystem.StoreView, i);
                if (block == null)
                    break;
                lstOfBlocks.Add(block.Header);
            }
            if (lstOfBlocks.Any() == false) return NoContent();
            return Ok(lstOfBlocks);
        }

        /// <summary>
        /// Gets the current block height of the connected node.
        /// </summary>
        /// <returns>Full Block Object.</returns>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpGet("blocks/height", Name = "GetBlockHeight")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Block))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
        public IActionResult GetCurrentBlock()
        {
            var currentIndex = NativeContract.Ledger.CurrentIndex(_neosystem.StoreView);
            var block = NativeContract.Ledger.GetHeader(_neosystem.StoreView, currentIndex);
            return Ok(block);
        }

        /// <summary>
        /// Gets a block by an its index.
        /// </summary>
        /// <param name="blockIndex" example="0">Block Index</param>
        /// <returns>Full Block Object.</returns>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpGet("blocks/{index:min(0)}", Name = "GetBlock")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Block))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
        public IActionResult GetBlock(
            [FromRoute(Name = "index")]
            uint blockIndex)
        {
            var block = NativeContract.Ledger.GetBlock(_neosystem.StoreView, blockIndex);
            if (block == null)
                throw new BlockNotFoundException(blockIndex);
            return Ok(block);
        }

        /// <summary>
        /// Gets a block header by block index.
        /// </summary>
        /// <param name="blockIndex" example="0">Blocks index.</param>
        /// <returns>Block Header Object.</returns>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpGet("blocks/{index:min(0)}/header", Name = "GetBlockHeader")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Header))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
        public IActionResult GetBlockHeader(
            [FromRoute(Name = "index")]
            uint blockIndex)
        {
            var block = NativeContract.Ledger.GetBlock(_neosystem.StoreView, blockIndex);
            if (block == null)
                throw new BlockNotFoundException(blockIndex);
            return Ok(block.Header);
        }

        /// <summary>
        /// Gets the witness of the block
        /// </summary>
        /// <param name="blockIndex" example="0">Block Index.</param>
        /// <returns>Witness Object</returns>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpGet("blocks/{index:min(0)}/witness", Name = "GetBlockWitness")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Witness))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
        public IActionResult GetBlockWitness(
            [FromRoute(Name = "index")]
            uint blockIndex)
        {
            var block = NativeContract.Ledger.GetBlock(_neosystem.StoreView, blockIndex);
            if (block == null)
                throw new BlockNotFoundException(blockIndex);
            return Ok(block.Witness);
        }

        /// <summary>
        /// Gets the transactions of the block.
        /// </summary>
        /// <param name="blockIndex" example="0">Block Index.</param>
        /// <param name="skip">Page</param>
        /// <param name="take">Page Size</param>
        /// <returns>An array of transaction object.</returns>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpGet("blocks/{index:min(0)}/transactions", Name = "GetBlockTransactions")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Transaction[]))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
        public IActionResult GetBlockTransactions(
            [FromRoute(Name = "index")]
            uint blockIndex,
            [FromQuery(Name = "page")]
            int skip = 1,
            [FromQuery(Name = "size")]
            int take = 1)
        {
            if (skip < 1 || take < 1 || take > _settings.MaxPageSize)
                throw new InvalidParameterRangeException();
            var block = NativeContract.Ledger.GetBlock(_neosystem.StoreView, blockIndex);
            if (block == null)
                throw new BlockNotFoundException(blockIndex);
            if (block.Transactions == null || block.Transactions.Length == 0) return NoContent();
            return Ok(block.Transactions.Skip((skip - 1) * take).Take(take));
        }

        #endregion

        #region Transactions

        /// <summary>
        /// Gets a transaction
        /// </summary>
        /// <param name="hash" example="0xad83d993ca2d9783ca86a000b39920c20508c8ccae7b7db11806646a4832bc50">Hash256</param>
        /// <returns>Transaction object.</returns>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpGet("transactions/{hash:required}", Name = "GetTransaction")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Transaction))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
        public IActionResult GetTransaction(
            [FromRoute( Name = "hash")]
            UInt256 hash)
        {
            if (NativeContract.Ledger.ContainsTransaction(_neosystem.StoreView, hash) == false) return NotFound();
            var txst = NativeContract.Ledger.GetTransaction(_neosystem.StoreView, hash);
            if (txst == null)
                throw new TransactionNotFoundException(hash);
            return Ok(txst);
        }

        /// <summary>
        /// Gets the witness of a transaction.
        /// </summary>
        /// <param name="hash" example="0xad83d993ca2d9783ca86a000b39920c20508c8ccae7b7db11806646a4832bc50">Hash256</param>
        /// <returns>An array of witness object.</returns>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpGet("transactions/{hash:required}/witnesses", Name = "GetTransactionWitnesses")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Witness[]))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
        public IActionResult GetTransactionWitnesses(
            [FromRoute( Name = "hash")]
            UInt256 hash)
        {
            if (NativeContract.Ledger.ContainsTransaction(_neosystem.StoreView, hash) == false)
                throw new TransactionNotFoundException(hash);
            var tx = NativeContract.Ledger.GetTransaction(_neosystem.StoreView, hash);
            return Ok(tx.Witnesses);
        }

        /// <summary>
        /// Gets the signers of a transaction.
        /// </summary>
        /// <param name="hash" example="0xad83d993ca2d9783ca86a000b39920c20508c8ccae7b7db11806646a4832bc50">Hash256</param>
        /// <returns>An array of Signer object.</returns>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpGet("transactions/{hash:required}/signers", Name = "GetTransactionSigners")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Signer[]))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
        public IActionResult GetTransactionSigners(
            [FromRoute( Name = "hash")]
            UInt256 hash)
        {
            if (NativeContract.Ledger.ContainsTransaction(_neosystem.StoreView, hash) == false)
                throw new TransactionNotFoundException(hash);
            var tx = NativeContract.Ledger.GetTransaction(_neosystem.StoreView, hash);
            return Ok(tx.Signers);
        }

        /// <summary>
        /// Gets the transaction attributes of a transaction.
        /// </summary>
        /// <param name="hash" example="0xad83d993ca2d9783ca86a000b39920c20508c8ccae7b7db11806646a4832bc50">Hash256</param>
        /// <returns>An array of the transaction attributes object.</returns>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpGet("transactions/{hash:required}/attributes", Name = "GetTransactionAttributes")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TransactionAttribute[]))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
        public IActionResult GetTransactionAttributes(
            [FromRoute( Name = "hash")]
            UInt256 hash)
        {
            if (NativeContract.Ledger.ContainsTransaction(_neosystem.StoreView, hash) == false)
                throw new TransactionNotFoundException(hash);
            var tx = NativeContract.Ledger.GetTransaction(_neosystem.StoreView, hash);
            return Ok(tx.Attributes);
        }

        #endregion

        #region Memory Pool

        /// <summary>
        /// Gets memory pool.
        /// </summary>
        /// <param name="skip" example="1">Page</param>
        /// <param name="take" example="50">Page Size.</param>
        /// <returns>An array of the Transaction object.</returns>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpGet("memorypool", Name = "GetMemoryPoolTransactions")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Transaction[]))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
        public IActionResult GetMemoryPool(
            [FromQuery(Name = "page")]
            int skip = 1,
            [FromQuery(Name = "size")]
            int take = 1)
        {
            if (skip < 0 || take < 0 || take > _settings.MaxPageSize)
                throw new InvalidParameterRangeException();
            return Ok(_neosystem.MemPool.Skip((skip - 1) * take).Take(take));
        }

        /// <summary>
        /// Gets the count of the memory pool.
        /// </summary>
        /// <returns>Memory Pool Count Object.</returns>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpGet("memorypool/count", Name = "GetMemoryPoolCount")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MemoryPoolCountModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
        public IActionResult GetMemoryPoolCount() =>
            Ok(new MemoryPoolCountModel()
            {
                Count = _neosystem.MemPool.Count,
                UnVerifiedCount = _neosystem.MemPool.UnVerifiedCount,
                VerifiedCount = _neosystem.MemPool.VerifiedCount,
            });

        /// <summary>
        /// Gets verified memory pool.
        /// </summary>
        /// <param name="skip" example="1">Page</param>
        /// <param name="take" example="50">Page Size.</param>
        /// <returns>An array of the Transaction object.</returns>
        /// <response code="204">No more pages.</response>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpGet("memorypool/verified", Name = "GetMemoryPoolVeridiedTransactions")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Transaction[]))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
        public IActionResult GetMemoryPoolVerified(
            [FromQuery(Name = "page")]
            int skip = 1,
            [FromQuery(Name = "size")]
            int take = 1)
        {
            if (skip < 0 || take < 0 || take > _settings.MaxPageSize)
                throw new InvalidParameterRangeException();
            if (_neosystem.MemPool.Any() == false) return NoContent();
            var vTx = _neosystem.MemPool.GetVerifiedTransactions();
            return Ok(vTx.Skip((skip - 1) * take).Take(take));
        }

        /// <summary>
        /// Gets unverified memory pool.
        /// </summary>
        /// <param name="skip" example="1">Page</param>
        /// <param name="take" example="50">Page Size.</param>
        /// <returns>An array of the Transaction object.</returns>
        /// <response code="204">No more pages.</response>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpGet("memorypool/unverified", Name = "GetMemoryPoolUnveridiedTransactions")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Transaction[]))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
        public IActionResult GetMemoryPoolUnVerified(
            [FromQuery(Name = "page")]
            int skip = 1,
            [FromQuery(Name = "size")]
            int take = 1)
        {
            if (skip < 0 || take < 0 || take > _settings.MaxPageSize)
                throw new InvalidParameterRangeException();
            if (_neosystem.MemPool.Any() == false) return NoContent();
            _neosystem.MemPool.GetVerifiedAndUnverifiedTransactions(out _, out var unVerifiedTransactions);
            return Ok(unVerifiedTransactions.Skip((skip - 1) * take).Take(take));
        }

        #endregion
    }
}
