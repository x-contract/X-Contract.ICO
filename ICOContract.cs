/**************************************************************************************************************************
 * The X-Contract foundation is a organzation of dedicating on smart contract evolution.
 * This smart contract is used to issue X-Contract coins, XCC.
 * X-Contract only accepts NEO donation. Donators have to transfer NEO to specified NEO address.
 * Once transcation has been confirmed, smart contract will trasnfer XCC to original address.
 * X-Contract website: http://www.x-contract.org
 * Author： Michael Li
 * Date: 12/April/2018
 * ************************************************************************************************************************/

using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.Numerics;

namespace X_Contract.ICO
{
    public class ICOContract : SmartContract
    {
        #region --- Constants ---
        /// <summary>
        /// The NEO Asset ID
        /// The NEO Governing Token: c56f33fc6ecfcd0c225c4ab356fee59390af8560be0e930faebe74a6daff7c9b
        /// </summary>
        private static readonly byte[] _NEO_Asset_ID = 
            Neo.SmartContract.Framework.Helper.HexToBytes("b9c7ffad6a47beeaf039e0eb0658fa09395eef635ba4c522c0dcfce6cf33f65c");
        /// <summary>
        /// The Gas Asset ID
        /// The Gas Token: 602c79718b16e442de58778e148d0b1084e3b2dffd5de6b7b16cee7969282de7
        /// </summary>
        private static readonly byte[] _Gas_Asset_ID = 
            Neo.SmartContract.Framework.Helper.HexToBytes("e72d286979ee6cb1b7e65dfddfb2e384100b8d148e7758de42e4168b71792c60");
        /// <summary>
        /// The name of X-Contract coin.
        /// </summary>
        private static string _coinName = "X-Contract Coin";
        /// <summary>
        /// The symbol of X-Contract coin.s
        /// </summary>
        private static string _coinSymbol = "XCC";
        /// <summary>
        /// Precision
        /// </summary>
        private static byte _coinPrecision = 8;

        private static uint _ratio = 1500;
        #endregion

        /// <summary>
        /// 
        /// </summary>
        public static object Main(string method, object[] args)
        {
            if (TriggerType.Application == Runtime.Trigger)
            {
                method = method.ToLower().Trim();
                switch (method)
                {
                    case "totalsupply":
                        return TotalSupply();
                    case "name":
                        return Name();
                    case "symbol":
                        return Symbol();
                    case "decimals":
                        return Decimals();
                    case "balanceof":
                        if (args.Length != 1)
                            return 0;
                        else
                            return BalanceOf((byte[])args[0]);
                    case "transfer":
                        {
                            if (3 != args.Length)
                                return false;
                            // Retrieve transfer parameters
                            byte[] from = (byte[])args[0];
                            byte[] to = (byte[])args[1];
                            if (from == to)
                                return true;
                            if (0 == from.Length || 0 == to.Length)
                                return false;
                            BigInteger value = (BigInteger)args[2];

                            // Check from address whether has been signed.
                            if (!Runtime.CheckWitness(from))
                                return false;
                            // Check whether is a springboard calling.
                            if (ExecutionEngine.EntryScriptHash.AsBigInteger()
                                != ExecutionEngine.CallingScriptHash.AsBigInteger())
                                return false;
                            // Do transaction.
                            return Transfer(from, to, value);
                        }
                    case "transfer_app":
                        {
                            if (3 != args.Length)
                                return false;
                            // Retrieve parameters
                            byte[] from = (byte[])args[0];
                            byte[] to = (byte[])args[1];
                            BigInteger value = (BigInteger)args[2];

                            // Check whether from address is script.
                            if (from.AsBigInteger() != ExecutionEngine.CallingScriptHash.AsBigInteger())
                                return false;
                            // Do transaction.
                            return Transfer(from, to, value);
                        }
                    case "mintTokens":
                        {
                            if (0 != args.Length)
                                return 0;
                            return MintTokens();
                        }
                    case "refund":
                        {
                            if (1 != args.Length)
                                return 0;
                            byte[] address = (byte[])args[0];
                            if (!Runtime.CheckWitness(address))
                                return false;
                            return Refund(address);
                        }
                    case "getRefundTarget":
                        {
                            if (1 != args.Length)
                                return 0;
                            byte[] hash = (byte[])args[0];
                            return GetTargetByTransactionID(hash);
                        }
                    default:
                        return false;
                }
            }
            // To get NEO from ICO address.
            else if (TriggerType.Verification == Runtime.Trigger)
            {
                Transaction trans = ExecutionEngine.ScriptContainer as Transaction;
                byte[] scriptHash = ExecutionEngine.ExecutingScriptHash;
                TransactionInput[] inputs = trans.GetInputs();
                TransactionOutput[] outputs = trans.GetOutputs();
                for (int i = 0; i < inputs.Length; i++)
                {
                    byte[] coinid = inputs[i].PrevHash.Concat(new byte[] { 0, 0 });
                    // UTXO euqals zero.
                    if (0 == inputs[i].PrevIndex)
                    {
                        byte[] target = Storage.Get(Storage.CurrentContext, coinid);
                        if (target.Length > 0)
                        {
                            // Check whether only one output address in current transaction.
                            if (outputs.Length != 1)
                                return false;

                            // Check whether withdraw address is target address.
                            // This is really important to guarantee coins safe.
                            if (outputs[0].ScriptHash.AsBigInteger() == target.AsBigInteger())
                            {
                                return true;
                            }
                            else
                            {
                                return false;
                            }
                        }
                    }
                }
            }
            
            return false;
        }

        #region --- Methods Implementations ---
        /// <summary>
        /// Get total supply amount.
        /// </summary>
        /// <returns></returns>
        private static BigInteger TotalSupply()
        {
            return Storage.Get(Storage.CurrentContext, "totalSupply").AsBigInteger();
        }
        /// <summary>
        /// Get Coin name.
        /// </summary>
        /// <returns></returns>
        private static string Name()
        {
            return _coinName;
        }
        /// <summary>
        /// Get Coin symbol.
        /// </summary>
        /// <returns></returns>
        public static string Symbol()
        {
            return _coinSymbol;
        }
        /// <summary>
        /// Get Precision
        /// </summary>
        /// <returns></returns>
        public static byte Decimals()
        {
            return _coinPrecision;
        }
        /// <summary>
        /// Get balance amount to specified address.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        private static BigInteger BalanceOf(byte[] address)
        {
            return Storage.Get(Storage.CurrentContext, address).AsBigInteger();
        }
        /// <summary>
        /// Asset transfer from address to donation address.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private static bool Transfer(byte[] from, byte[] to, BigInteger value)
        {
            if (value <= 0) return false;
            if (from == to) return true;

            // Someone who wants to donate NEO coins.
            if (from.Length > 0)
            {
                BigInteger from_value = Storage.Get(Storage.CurrentContext, from).AsBigInteger();
                if (from_value < value) return false;
                if (from_value == value)
                    Storage.Delete(Storage.CurrentContext, from);
                else
                    Storage.Put(Storage.CurrentContext, from, from_value - value);
            }
            // The receive address.
            if (to.Length > 0)
            {
                BigInteger to_value = Storage.Get(Storage.CurrentContext, to).AsBigInteger();
                Storage.Put(Storage.CurrentContext, to, to_value + value);
            }
            //Log transaction information into block.
            PutTransactionInfo(from, to, value);
            return true;
        }
        /// <summary>
        /// Log transaction information into block.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="value"></param>
        private static void PutTransactionInfo(byte[] from, byte[] to, BigInteger value)
        {
            byte[] transBytes = SerializeTransaction(from, to, value);
            if (null != transBytes)
            {
                byte[] transactionID = (ExecutionEngine.ScriptContainer as Transaction).Hash;
                Storage.Put(Storage.CurrentContext, transactionID, transBytes);
            }
        }
        /// <summary>
        /// Issue tokens to donators.
        /// </summary>
        /// <returns></returns>
        private static bool MintTokens()
        {
            Transaction trans = ExecutionEngine.ScriptContainer as Transaction;

            // Get dotnator address.
            byte[] donatorAddress = null;
            TransactionOutput[] reference = trans.GetReferences();
            for (var i = 0; i < reference.Length; i++)
            {
                if (reference[i].AssetId.AsBigInteger() == _NEO_Asset_ID.AsBigInteger())
                {
                    donatorAddress = reference[i].ScriptHash;
                    break;
                }
            }

            TransactionOutput[] outputs = trans.GetOutputs();
            ulong value = 0;
            // Get amount of input NEO.
            foreach (TransactionOutput output in outputs)
            {
                if (output.ScriptHash == ExecutionEngine.ExecutingScriptHash &&
                    output.AssetId.AsBigInteger() == _NEO_Asset_ID.AsBigInteger())
                {
                    value += (ulong)output.Value;
                }
            }

            //Calcuate XCC amount by NEO.
            ulong XCCAmount = value * _ratio;
            var TotalSupply = Storage.Get(Storage.CurrentContext, "totalSupply").AsBigInteger();
            TotalSupply += XCCAmount;
            Storage.Put(Storage.CurrentContext, "totalSupply", TotalSupply);

            // Issue XCC token to donators.
            return Transfer(null, donatorAddress, XCCAmount);

        }
        /// <summary>
        /// Refund NEO to donators.
        /// </summary>
        /// <param name="who"></param>
        /// <returns></returns>
        private static bool Refund(byte[] who)
        {
            Transaction trans = ExecutionEngine.ScriptContainer as Transaction;
            TransactionOutput[] outputs = trans.GetOutputs();
            if (outputs[0].AssetId.AsBigInteger() != _NEO_Asset_ID.AsBigInteger())
                return false;
            // Refund to self.
            if (outputs[0].ScriptHash.AsBigInteger() != ExecutionEngine.ExecutingScriptHash.AsBigInteger())
                return false;

            // Check Whether transaction has already exists.
            // If yes, it cannot refund.
            byte[] target = GetTargetByTransactionID(trans.Hash);
            if (target.Length > 0)
                return false;

            // Destory XCC that has been issued.
            long count = outputs[0].Value;
            if(!Transfer(who, null, count))
                return false;

            // Set UTXO.
            byte[] coinID = trans.Hash.Concat(new byte[] { 0, 0 });
            Storage.Put(Storage.CurrentContext, coinID, who);
            // Fix the amount of total amount.
            var totalSupply = Storage.Get(Storage.CurrentContext, "totalSupply").AsBigInteger();

            totalSupply -= count;
            Storage.Put(Storage.CurrentContext, "totalSupply", totalSupply);

            return true;
        }
        /// <summary>
        /// Retrieve target by Transaction ID.
        /// </summary>
        /// <param name="transactionID"></param>
        /// <returns></returns>
        private static byte[] GetTargetByTransactionID(byte[] transactionID)
        {
            byte[] coinID = transactionID.Concat(new byte[] { 0, 0 });
            byte[] target = Storage.Get(Storage.CurrentContext, transactionID);
            return target;
        }
        #endregion


        #region --- Private Functions ---
        /// <summary>
        /// Serialize transaction data to bytes array.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private static byte[] SerializeTransaction(byte[] from, byte[] to, BigInteger value)
        {
            if (null == from || null == to)
                return null;
            byte[] valueBytes = value.ToByteArray();
            byte[] buffer = new byte[from.Length + to.Length + valueBytes.Length];
            from.CopyTo(buffer, 0);
            to.CopyTo(buffer, from.Length);
            valueBytes.CopyTo(buffer, from.Length + to.Length);
            return buffer;
        }
        #endregion
    }
}
