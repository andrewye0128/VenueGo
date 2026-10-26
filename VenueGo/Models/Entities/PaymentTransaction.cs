using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class PaymentTransaction
{
    public int PaymentTransactionId { get; set; }

    public int PaymentId { get; set; }

    public string MerchantTradeNo { get; set; } = null!;

    public byte EventType { get; set; }

    public string? TradeNo { get; set; }

    public int? RtnCode { get; set; }

    public string? RtnMsg { get; set; }

    public int? TradeAmount { get; set; }

    public DateTime? EcpayPaymentDate { get; set; }

    public bool? IsMacValid { get; set; }

    public byte ProcessResult { get; set; }

    public string? RawData { get; set; }

    public DateTime CreatedAt { get; set; }
}