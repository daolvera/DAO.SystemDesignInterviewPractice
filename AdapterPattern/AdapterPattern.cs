// ============================================
// ADAPTER PATTERN - Real World Example
// ============================================
// Scenario: E-commerce system integrating with different payment gateways
// (PayPal and Stripe) that have incompatible APIs.

using System;

namespace AdapterPattern.PaymentGateway
{
    // ============================================
    // TARGET INTERFACE - What our app expects
    // ============================================
    public interface IPaymentProcessor
    {
        bool ProcessPayment(decimal amount, string currency, string customerEmail);
        bool RefundPayment(string transactionId, decimal amount);
    }

    // ============================================
    // CLIENT - Checkout Service (uses only IPaymentProcessor)
    // ============================================
    public class CheckoutService
    {
        private readonly IPaymentProcessor _processor;

        public CheckoutService(IPaymentProcessor processor) => _processor = processor;

        public void CompleteOrder(decimal total, string currency, string email)
        {
            Console.WriteLine($"\n💳 Processing {total:C}...");
            var success = _processor.ProcessPayment(total, currency, email);
            Console.WriteLine(success ? "✅ Payment successful!\n" : "❌ Payment failed!\n");
        }
    }

    // ============================================
    // DEMO
    // ============================================
    public class Program
    {
        public static void Main()
        {
            Console.WriteLine("═══ ADAPTER PATTERN DEMO ═══\n");

            // PayPal checkout
            var paypalCheckout = new CheckoutService(new PayPalAdapter(new PayPalSdk()));
            paypalCheckout.CompleteOrder(99.99m, "USD", "user1@email.com");

            // Stripe checkout - same client code, different adapter!
            var stripeCheckout = new CheckoutService(new StripeAdapter(new StripeApi()));
            stripeCheckout.CompleteOrder(149.50m, "EUR", "user2@email.com");
        }
    }
}
