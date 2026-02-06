using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AdapterPattern.PaymentGateway
{
    // ============================================
    // ADAPTEE #2 - Stripe's API (different interface)
    // ============================================
    public class StripeApi
    {
        public StripeCharge CreateCharge(int amountInCents, string currency, string email)
        {
            Console.WriteLine($"[Stripe] Charging {amountInCents} cents ({currency}) to {email}");
            return new StripeCharge
            {
                Id = $"ch_{Guid.NewGuid().ToString().Substring(0, 8)}",
                Status = "succeeded",
            };
        }

        public StripeRefund CreateRefund(string chargeId, int amountInCents)
        {
            Console.WriteLine($"[Stripe] Refunding {amountInCents} cents for {chargeId}");
            return new StripeRefund { Status = "succeeded" };
        }
    }

    public class StripeCharge
    {
        public string Id { get; set; }
        public string Status { get; set; }
    }

    public class StripeRefund
    {
        public string Status { get; set; }
    }

    // ============================================
    // ADAPTER #2 - Stripe Adapter
    // ============================================
    public class StripeAdapter : IPaymentProcessor
    {
        private readonly StripeApi _stripe;

        public StripeAdapter(StripeApi stripe) => _stripe = stripe;

        public bool ProcessPayment(decimal amount, string currency, string customerEmail)
        {
            // Adapt: dollars (decimal) → cents (int)
            int cents = (int)(amount * 100);
            var charge = _stripe.CreateCharge(cents, currency.ToLower(), customerEmail);
            return charge.Status == "succeeded";
        }

        public bool RefundPayment(string transactionId, decimal amount)
        {
            var refund = _stripe.CreateRefund(transactionId, (int)(amount * 100));
            return refund.Status == "succeeded";
        }
    }
}
