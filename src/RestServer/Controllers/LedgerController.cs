using Microsoft.AspNetCore.Mvc;
using Neo.SmartContract.Native;
using Microsoft.AspNetCore.Http;
using Neo.Plugins.RestServer.Models;
using Neo.Plugins.RestServer.Extensions;

namespace Neo.Plugins.Controllers
{
    [Route("/api/v1/ledger")]
    public class LedgerController : ControllerBase
    {
        private const int _max_page_size = 50;

        private readonly NeoSystem _neosystem;

        public LedgerController(
            NeoSystem neoSystem)
        {
            _neosystem = neoSystem;
        }

        #region Blocks

        [HttpGet("blocks")]
        public IActionResult GetBlocks(
            [FromQuery(Name = "page")]
            uint skip = 1,
            [FromQuery(Name = "size")]
            uint take = 1)
        {
            if (skip < 1 || take < 1 || take > _max_page_size) return StatusCode(StatusCodes.Status416RequestedRangeNotSatisfiable);
            //var start = (skip - 1) * take + startIndex;
            //var end = start + take;
            var start = NativeContract.Ledger.CurrentIndex(_neosystem.StoreView) - ((skip - 1) * take);
            var end =  start - take;
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

        [HttpGet("blocks/height")]
        public IActionResult GetCurrentBlock()
        {
            var currentIndex = NativeContract.Ledger.CurrentIndex(_neosystem.StoreView);
            var block = NativeContract.Ledger.GetHeader(_neosystem.StoreView, currentIndex);
            return Ok(block.ToModel());
        }

        [HttpGet("blocks/{index:min(0)}")]
        public IActionResult GetBlock(
            [FromRoute(Name = "index")]
            uint blockIndex)
        {
            var block = NativeContract.Ledger.GetBlock(_neosystem.StoreView, blockIndex);
            if (block == null) return NotFound();
            return Ok(block.ToModel());
        }

        [HttpGet("blocks/{index:min(0)}/header")]
        public IActionResult GetBlockHeader(
            [FromRoute(Name = "index")]
            uint blockIndex)
        {
            var block = NativeContract.Ledger.GetBlock(_neosystem.StoreView, blockIndex);
            if (block == null) return NotFound();
            return Ok(block.Header.ToModel());
        }

        [HttpGet("blocks/{index:min(0)}/witness")]
        public IActionResult GetBlockWitness(
            [FromRoute(Name = "index")]
            uint blockIndex)
        {
            var block = NativeContract.Ledger.GetBlock(_neosystem.StoreView, blockIndex);
            if (block == null) return NotFound();
            return Ok(block.Witness.ToModel());
        }

        [HttpGet("blocks/{index:min(0)}/transactions")]
        public IActionResult GetBlockTransactions(
            [FromRoute(Name = "index")]
            uint blockIndex,
            [FromQuery(Name = "page")]
            int skip = 1,
            [FromQuery(Name = "size")]
            int take = 1)
        {
            if (skip < 1 || take < 1 || take > _max_page_size) return StatusCode(StatusCodes.Status416RequestedRangeNotSatisfiable);
            var block = NativeContract.Ledger.GetBlock(_neosystem.StoreView, blockIndex);
            if (block == null) return NotFound();
            if (block.Transactions == null || block.Transactions.Length == 0) return NoContent();
            return Ok(block.Transactions.Skip((skip -1) * take).Take(take).Select(s => s.ToModel()));
        }

        #endregion

        #region Transactions

        [HttpGet("transactions/{hash:required}")]
        public IActionResult GetTransaction(
            [FromRoute( Name = "hash")]
            string txHash)
        {
            if (UInt256.TryParse(txHash, out var hash) == false) return BadRequest();
            if (NativeContract.Ledger.ContainsTransaction(_neosystem.StoreView, hash) == false) return NotFound();
            var txst = NativeContract.Ledger.GetTransaction(_neosystem.StoreView, hash);
            if (txst == null) return NotFound();
            return Ok(txst.ToModel());
        }

        [HttpGet("transactions/{hash:required}/witnesses")]
        public IActionResult GetTransactionWitnesses(
            [FromRoute( Name = "hash")]
            string txHash)
        {
            if (UInt256.TryParse(txHash, out var hash) == false) return BadRequest();
            if (NativeContract.Ledger.ContainsTransaction(_neosystem.StoreView, hash) == false) return NotFound();
            var tx = NativeContract.Ledger.GetTransaction(_neosystem.StoreView, hash);
            return Ok(tx.Witnesses.Select(s => s.ToModel()));
        }

        [HttpGet("transactions/{hash:required}/signers")]
        public IActionResult GetTransactionSigners(
            [FromRoute( Name = "hash")]
            string txHash)
        {
            if (UInt256.TryParse(txHash, out var hash) == false) return BadRequest();
            if (NativeContract.Ledger.ContainsTransaction(_neosystem.StoreView, hash) == false) return NotFound();
            var tx = NativeContract.Ledger.GetTransaction(_neosystem.StoreView, hash);
            return Ok(tx.Signers.Select(s => s.ToModel()));
        }

        [HttpGet("transactions/{hash:required}/attributes")]
        public IActionResult GetTransactionAttributes(
            [FromRoute( Name = "hash")]
            string txHash)
        {
            if (UInt256.TryParse(txHash, out var hash) == false) return BadRequest();
            if (NativeContract.Ledger.ContainsTransaction(_neosystem.StoreView, hash) == false) return NotFound();
            var tx = NativeContract.Ledger.GetTransaction(_neosystem.StoreView, hash);
            return Ok(tx.Attributes.Select(s => s.ToModel()));
        }

        #endregion

        #region Memory Pool

        [HttpGet("memorypool")]
        public IActionResult GetMemoryPool(
            [FromQuery(Name = "page")]
            int skip = 1,
            [FromQuery(Name = "size")]
            int take = 1)
        {
            if (skip < 0 || take < 0 || take > _max_page_size) return StatusCode(StatusCodes.Status416RequestedRangeNotSatisfiable);
            if (_neosystem.MemPool.Any() == false) return NoContent();
            return Ok(_neosystem.MemPool.Skip((skip - 1) * take).Take(take).Select(s => s.ToModel()));
        }

        [HttpGet("memorypool/count")]
        public IActionResult GetMemoryPoolCount() =>
            Ok(new
            {
                _neosystem.MemPool.Count,
                _neosystem.MemPool.UnVerifiedCount,
                _neosystem.MemPool.VerifiedCount,
            });

        [HttpGet("memorypool/verified")]
        public IActionResult GetMemoryPoolVerified(
            [FromQuery(Name = "page")]
            int skip = 1,
            [FromQuery(Name = "size")]
            int take = 1)
        {
            if (skip < 0 || take < 0 || take > _max_page_size) return StatusCode(StatusCodes.Status416RequestedRangeNotSatisfiable);
            if (_neosystem.MemPool.Any() == false) return NoContent();
            var vTx = _neosystem.MemPool.GetVerifiedTransactions();
            if (vTx.Any() == false) return NoContent();
            return Ok(vTx.Skip((skip - 1) * take).Take(take).Select(s => s.ToModel()));
        }

        [HttpGet("memorypool/unverified")]
        public IActionResult GetMemoryPoolUnVerified(
            [FromQuery(Name = "page")]
            int skip = 1,
            [FromQuery(Name = "size")]
            int take = 1)
        {
            if (skip < 0 || take < 0 || take > _max_page_size) return StatusCode(StatusCodes.Status416RequestedRangeNotSatisfiable);
            if (_neosystem.MemPool.Any() == false) return NoContent();
            _neosystem.MemPool.GetVerifiedAndUnverifiedTransactions(out _, out var unVerifiedTransactions);
            if (unVerifiedTransactions.Any() == false) return NoContent();
            return Ok(unVerifiedTransactions.Skip((skip - 1) * take).Take(take).Select(s => s.ToModel())
            );
        }

        #endregion
    }
}
