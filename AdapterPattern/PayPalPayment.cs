using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AdapterPattern.PaymentGateway
{
    // ============================================
    // ADAPTEE #1 - PayPal's Legacy SDK (can't modify)
    // ============================================
    public class PayPalSdk
    {
        public string MakePayment(double amountInUsd, string buyerEmail)
        {
            Console.WriteLine($"[PayPal] Processing ${amountInUsd} for {buyerEmail}");
            return $"PP-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
        }

        public bool ExecuteRefund(string transactionId, double amount)
        {
            Console.WriteLine($"[PayPal] Refunding ${amount} for {transactionId}");
            return true;
        }
    }

    // ============================================
    // ADAPTER #1 - PayPal Adapter
    // ============================================
    public class PayPalAdapter : IPaymentProcessor
    {
        private readonly PayPalSdk _payPal;

        public PayPalAdapter(PayPalSdk payPal) => _payPal = payPal;

        public bool ProcessPayment(decimal amount, string currency, string customerEmail)
        {
            if (currency != "USD")
            {
                Console.WriteLine("[PayPal] Currency conversion required. Only USD is supported.");
                return false;
            }
            // Adapt: decimal → double, assume USD conversion
            var txnId = _payPal.MakePayment((double)amount, customerEmail);
            return !string.IsNullOrEmpty(txnId);
        }

        public bool RefundPayment(string transactionId, decimal amount)
        {
            return _payPal.ExecuteRefund(transactionId, (double)amount);
        }
    }
}
