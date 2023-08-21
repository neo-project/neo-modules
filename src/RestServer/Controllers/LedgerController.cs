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
using Neo.Plugins.RestServer.Models;
using Neo.Plugins.RestServer.Extensions;
using Neo.Plugins.RestServer.Exceptions;
using System.Net.Mime;

namespace Neo.Plugins.RestServer.Controllers
{
    [Route("/api/v1/ledger")]
    [Produces(MediaTypeNames.Application.Json)]
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

        [HttpGet("gas/accounts", Name = "GetGasAccounts")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult ShowGasAccounts()
        {
            var accounts = NativeContract.GAS.ListAccounts(_neosystem.StoreView, _neosystem.Settings);
            return Ok(accounts.OrderByDescending(o => o.Balance));
        }

        [HttpGet("neo/accounts", Name = "GetNeoAccounts")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult ShowNeoAccounts()
        {
            var accounts = NativeContract.NEO.ListAccounts(_neosystem.StoreView, _neosystem.Settings);
            return Ok(accounts.OrderByDescending(o => o.Balance));
        }

        #endregion

        #region Blocks

        [HttpGet("blocks", Name = "GetBlocks")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
            var lstOfBlocks = new List<BlockHeaderModel>();
            for (uint i = start; i > end; i--)
            {
                var block = NativeContract.Ledger.GetBlock(_neosystem.StoreView, i);
                if (block == null)
                    break;
                lstOfBlocks.Add(block.ToHeaderModel());
            }
            if (lstOfBlocks.Any() == false) return NoContent();
            return Ok(lstOfBlocks);
        }

        [HttpGet("blocks/height", Name = "GetBlockHeight")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GetCurrentBlock()
        {
            var currentIndex = NativeContract.Ledger.CurrentIndex(_neosystem.StoreView);
            var block = NativeContract.Ledger.GetHeader(_neosystem.StoreView, currentIndex);
            return Ok(block.ToModel());
        }

        [HttpGet("blocks/{index:min(0)}", Name = "GetBlock")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GetBlock(
            [FromRoute(Name = "index")]
            uint blockIndex)
        {
            var block = NativeContract.Ledger.GetBlock(_neosystem.StoreView, blockIndex);
            if (block == null)
                throw new BlockNotFoundException(blockIndex);
            return Ok(block.ToModel());
        }

        [HttpGet("blocks/{index:min(0)}/header", Name = "GetBlockHeader")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GetBlockHeader(
            [FromRoute(Name = "index")]
            uint blockIndex)
        {
            var block = NativeContract.Ledger.GetBlock(_neosystem.StoreView, blockIndex);
            if (block == null)
                throw new BlockNotFoundException(blockIndex);
            return Ok(block.Header.ToModel());
        }

        [HttpGet("blocks/{index:min(0)}/witness", Name = "GetBlockWitness")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GetBlockWitness(
            [FromRoute(Name = "index")]
            uint blockIndex)
        {
            var block = NativeContract.Ledger.GetBlock(_neosystem.StoreView, blockIndex);
            if (block == null)
                throw new BlockNotFoundException(blockIndex);
            return Ok(block.Witness.ToModel());
        }

        [HttpGet("blocks/{index:min(0)}/transactions", Name = "GetBlockTransactions")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
            return Ok(block.Transactions.Skip((skip - 1) * take).Take(take).Select(s => s.ToModel()));
        }

        #endregion

        #region Transactions

        [HttpGet("transactions/{hash:required}", Name = "GetTransaction")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GetTransaction(
            [FromRoute( Name = "hash")]
            UInt256 hash)
        {
            if (NativeContract.Ledger.ContainsTransaction(_neosystem.StoreView, hash) == false) return NotFound();
            var txst = NativeContract.Ledger.GetTransaction(_neosystem.StoreView, hash);
            if (txst == null)
                throw new TransactionNotFoundException(hash);
            return Ok(txst.ToModel());
        }

        [HttpGet("transactions/{hash:required}/witnesses", Name = "GetTransactionWitnesses")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GetTransactionWitnesses(
            [FromRoute( Name = "hash")]
            UInt256 hash)
        {
            if (NativeContract.Ledger.ContainsTransaction(_neosystem.StoreView, hash) == false)
                throw new TransactionNotFoundException(hash);
            var tx = NativeContract.Ledger.GetTransaction(_neosystem.StoreView, hash);
            return Ok(tx.Witnesses.Select(s => s.ToModel()));
        }

        [HttpGet("transactions/{hash:required}/signers", Name = "GetTransactionSigners")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GetTransactionSigners(
            [FromRoute( Name = "hash")]
            UInt256 hash)
        {
            if (NativeContract.Ledger.ContainsTransaction(_neosystem.StoreView, hash) == false)
                throw new TransactionNotFoundException(hash);
            var tx = NativeContract.Ledger.GetTransaction(_neosystem.StoreView, hash);
            return Ok(tx.Signers.Select(s => s.ToModel()));
        }

        [HttpGet("transactions/{hash:required}/attributes", Name = "GetTransactionAttributes")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GetTransactionAttributes(
            [FromRoute( Name = "hash")]
            UInt256 hash)
        {
            if (NativeContract.Ledger.ContainsTransaction(_neosystem.StoreView, hash) == false)
                throw new TransactionNotFoundException(hash);
            var tx = NativeContract.Ledger.GetTransaction(_neosystem.StoreView, hash);
            return Ok(tx.Attributes.Select(s => s.ToModel()));
        }

        #endregion

        #region Memory Pool

        [HttpGet("memorypool", Name = "GetMemoryPoolTransactions")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GetMemoryPool(
            [FromQuery(Name = "page")]
            int skip = 1,
            [FromQuery(Name = "size")]
            int take = 1)
        {
            if (skip < 0 || take < 0 || take > _settings.MaxPageSize)
                throw new InvalidParameterRangeException();
            return Ok(_neosystem.MemPool.Skip((skip - 1) * take).Take(take).Select(s => s.ToModel()));
        }

        [HttpGet("memorypool/count", Name = "GetMemoryPoolCount")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GetMemoryPoolCount() =>
            Ok(new
            {
                _neosystem.MemPool.Count,
                _neosystem.MemPool.UnVerifiedCount,
                _neosystem.MemPool.VerifiedCount,
            });

        [HttpGet("memorypool/verified", Name = "GetMemoryPoolVeridiedTransactions")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
            return Ok(vTx.Skip((skip - 1) * take).Take(take).Select(s => s.ToModel()));
        }

        [HttpGet("memorypool/unverified", Name = "GetMemoryPoolUnveridiedTransactions")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
            return Ok(unVerifiedTransactions.Skip((skip - 1) * take).Take(take).Select(s => s.ToModel())
            );
        }

        #endregion
    }
}
