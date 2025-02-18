﻿using System;
using System.Web;
using System.Collections.Generic;
using System.Text;
using Mx.NET.SDK.WebWallet.Enums;
using Mx.NET.SDK.Configuration;
using Mx.NET.SDK.Domain;
using Mx.NET.SDK.Provider.Dtos.API.Transactions;

namespace Mx.NET.SDK.WebWallet
{
    public class WebWallet : IWebWallet
    {
        private const string WALLET_PROVIDER_CONNECT_URL = "hook/login";
        private const string WALLET_PROVIDER_DISCONNECT_URL = "hook/logout";
        private const string WALLET_PROVIDER_SEND_TRANSACTION_URL = "hook/transaction";
        private const string WALLET_PROVIDER_SIGN_TRANSACTION_URL = "hook/sign";

        private readonly MultiversxNetworkConfiguration _networkConfiguration;

        public WebWallet(MultiversxNetworkConfiguration configuration)
        {
            _networkConfiguration = configuration;
        }

        #region Create URL

        /// <summary>
        /// Create login URL
        /// </summary>
        /// <param name="callbackUrl">callback URL</param>
        /// <returns>URL</returns>
        public Uri CreateLoginUrl(string callbackUrl)
        {
            return new Uri($"{_networkConfiguration.WebWalletUri}{WALLET_PROVIDER_CONNECT_URL}?callbackUrl={callbackUrl}");
        }

        /// <summary>
        /// Create logout URL
        /// </summary>
        /// <param name="callbackUrl">callback URL</param>
        /// <returns>URL</returns>
        public Uri CreateLogoutUrl(string callbackUrl)
        {
            return new Uri($"{_networkConfiguration.WebWalletUri}{WALLET_PROVIDER_DISCONNECT_URL}?callbackUrl={callbackUrl}");
        }

        /// <summary>
        /// Create transaction URL
        /// </summary>
        /// <param name="transaction">Transaction Request</param>
        /// <param name="callbackUrl">callback URL</param>
        /// <returns></returns>
        public Uri CreateTransactionUrl(TransactionRequest transaction, string callbackUrl)
        {
            return new Uri($"{_networkConfiguration.WebWalletUri}{WALLET_PROVIDER_SEND_TRANSACTION_URL}?{BuildTransactionUrl(transaction)}&callbackUrl={callbackUrl}");
        }

        /// <summary>
        /// Create sign transaction URL
        /// </summary>
        /// <param name="transaction">Transaction Request</param>
        /// <param name="callbackUrl">callback URL</param>
        /// <returns></returns>
        public Uri CreateTransactionToSignUrl(TransactionRequest transaction, string callbackUrl)
        {
            return CreateTransactionsToSignUrl(new TransactionRequest[1] { transaction }, callbackUrl);
        }

        /// <summary>
        /// Create sign transactions URL
        /// </summary>
        /// <param name="transactions">Array of Transaction Requests</param>
        /// <param name="callbackUrl">callback URL</param>
        /// <returns></returns>
        public Uri CreateTransactionsToSignUrl(TransactionRequest[] transactions, string callbackUrl)
        {
            return new Uri($"{_networkConfiguration.WebWalletUri}{WALLET_PROVIDER_SIGN_TRANSACTION_URL}?{BuildTransactionsUrl(transactions)}&callbackUrl={callbackUrl}");
        }

        private string BuildTransactionUrl(TransactionRequest transaction)
        {
            return BuildTransactionsUrl(new TransactionRequest[1] { transaction });
        }

        private string BuildTransactionsUrl(TransactionRequest[] transactions)
        {
            var urlString = "";
            for (var i = 0; i < transactions.Length; i++)
            {
                var transaction = transactions[i].GetTransactionRequest();
                urlString += $"receiver[{i}]={transaction.Receiver}";
                urlString += $"&value[{i}]={transaction.Value}";
                urlString += $"&gasLimit[{i}]={transaction.GasLimit}";
                urlString += $"&gasPrice[{i}]={transaction.GasPrice}";
                urlString += $"&nonce[{i}]={transaction.Nonce}";
                urlString += $"&chainId[{i}]={transaction.ChainID}";
                urlString += $"&version[{i}]=1";
                if (transactions[i].Data != null)
                    urlString += $"&data[{i}]={Encoding.UTF8.GetString(Convert.FromBase64String(transaction.Data))}";

                if (i < transactions.Length - 1)
                    urlString += "&";
            }
            return urlString.Replace("[", "%5B").Replace("]", "%5D");
        }

        #endregion

        #region URL to Transaction

        /// <summary>
        /// Convert url paramters to Transactions
        /// </summary>
        /// <param name="urlString">URL parameters as string</param>
        /// <returns>Transactions</returns>
        public TransactionRequestDto[] GetTransactionsFromUrl(string urlString, out WalletTransactionStatus status)
        {
            if (urlString is null || urlString == "") throw new Exception("No URL provided");

            var idx = urlString.IndexOf('?');
            string query;
            if (idx == -1)
                query = urlString;
            else
                query = urlString.Substring(idx);

            var args = HttpUtility.ParseQueryString(query);
            if (args.Count == 0) throw new Exception("URL has no arguments");

            if (args["status"] == "cancelled")
            {
                status = WalletTransactionStatus.Cancelled;
                return Array.Empty<TransactionRequestDto>();
            }

            if (args["status"] != null && args["status"] != "transactionsSigned")
            {
                status = WalletTransactionStatus.Unknown;
                return Array.Empty<TransactionRequestDto>();
            }

            var transactions = new List<TransactionRequestDto>();
            for (var i = 0; i < args.Count / 10; i++)
                transactions.Add(new TransactionRequestDto()
                {
                    Nonce = ulong.Parse(args[$"nonce[{i}]"]),
                    Value = args[$"value[{i}]"],
                    Receiver = args[$"receiver[{i}]"],
                    Sender = args[$"sender[{i}]"],
                    GasPrice = long.Parse(args[$"gasPrice[{i}]"]),
                    GasLimit = long.Parse(args[$"gasLimit[{i}]"]),
                    Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(args[$"data[{i}]"])),
                    ChainID = args[$"chainId[{i}]"],
                    Version = int.Parse(args[$"version[{i}]"]),
                    Signature = args[$"signature[{i}]"],
                });

            status = WalletTransactionStatus.TransactionsSigned;
            return transactions.ToArray();
        }

        #endregion
    }
}
