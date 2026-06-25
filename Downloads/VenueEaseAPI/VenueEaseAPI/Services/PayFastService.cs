using System.Security.Cryptography;
using System.Text;
using VenueEaseAPI.Models;

namespace VenueEaseAPI.Services;

public interface IPayFastService
{
    string GeneratePaymentUrl(Booking booking, bool depositOnly = true);
    bool ValidateItn(Dictionary<string, string> itnData);
}

public class PayFastService : IPayFastService
{
    private readonly IConfiguration _config;

    public PayFastService(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Builds a PayFast payment URL. Redirect the client here to pay.
    /// Docs: https://developers.payfast.co.za/docs
    /// </summary>
    public string GeneratePaymentUrl(Booking booking, bool depositOnly = true)
    {
        var pf = _config.GetSection("PayFast");
        var merchantId = pf["MerchantId"]!;
        var merchantKey = pf["MerchantKey"]!;
        var passphrase = pf["Passphrase"];
        var isSandbox = bool.Parse(pf["Sandbox"] ?? "true");

        var amount = depositOnly ? booking.DepositAmount : booking.TotalAmount;
        var baseUrl = isSandbox
            ? "https://sandbox.payfast.co.za/eng/process"
            : "https://www.payfast.co.za/eng/process";

        var appBaseUrl = _config["AppSettings:BaseUrl"]!;

        // Build ordered parameter dictionary (PayFast requires alphabetical order for signature)
        var parameters = new SortedDictionary<string, string>
        {
            ["merchant_id"] = merchantId,
            ["merchant_key"] = merchantKey,
            ["return_url"] = $"{appBaseUrl}/api/payments/return",
            ["cancel_url"] = $"{appBaseUrl}/api/payments/cancel",
            ["notify_url"] = $"{appBaseUrl}/api/payments/notify",
            ["name_first"] = booking.ClientName.Split(' ')[0],
            ["name_last"] = booking.ClientName.Contains(' ')
                ? booking.ClientName[(booking.ClientName.IndexOf(' ') + 1)..]
                : "",
            ["email_address"] = booking.ClientEmail,
            ["m_payment_id"] = booking.BookingReference,
            ["amount"] = amount.ToString("F2"),
            ["item_name"] = $"Booking: {booking.Venue.Name}",
            ["item_description"] = $"{(depositOnly ? "Deposit" : "Full payment")} for {booking.StartDateTime:dd MMM yyyy}",
            ["custom_str1"] = booking.Id.ToString(),
            ["custom_str2"] = depositOnly ? "deposit" : "full",
        };

        // Generate signature
        var signature = GenerateSignature(parameters, passphrase);
        parameters["signature"] = signature;

        var queryString = string.Join("&",
            parameters.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        return $"{baseUrl}?{queryString}";
    }

    /// <summary>
    /// Validates the ITN (Instant Transaction Notification) from PayFast.
    /// Called by the notify webhook endpoint.
    /// </summary>
    public bool ValidateItn(Dictionary<string, string> itnData)
    {
        var pf = _config.GetSection("PayFast");
        var passphrase = pf["Passphrase"];

        // Step 1: Remove the signature from data before recalculating
        var dataWithoutSignature = new SortedDictionary<string, string>(itnData);
        dataWithoutSignature.Remove("signature");

        // Step 2: Recreate the signature
        var calculatedSignature = GenerateSignature(dataWithoutSignature, passphrase);

        // Step 3: Compare with received signature
        if (!itnData.TryGetValue("signature", out var receivedSignature))
            return false;

        return calculatedSignature.Equals(receivedSignature, StringComparison.OrdinalIgnoreCase);
    }

    private string GenerateSignature(SortedDictionary<string, string> parameters, string? passphrase)
    {
        var paramString = string.Join("&",
            parameters
                .Where(kv => !string.IsNullOrEmpty(kv.Value))
                .Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value).Replace("+", "%20")}"));

        if (!string.IsNullOrEmpty(passphrase))
            paramString += $"&passphrase={Uri.EscapeDataString(passphrase)}";

        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(paramString));
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }
}
