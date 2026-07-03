namespace NickeltownPOSV4.Models.Membership;

public static class MembershipDisplayFormatters
{
    public static string FormatPaymentStatus(MembershipPaymentStatus status) => status switch
    {
        MembershipPaymentStatus.AwaitingPayment => "Awaiting payment",
        MembershipPaymentStatus.Paid => "Paid",
        MembershipPaymentStatus.Complimentary => "Complimentary",
        _ => status.ToString(),
    };

    public static string FormatPaymentMethod(MembershipPaymentMethod? method) => method switch
    {
        MembershipPaymentMethod.Cash => "Cash",
        MembershipPaymentMethod.EFT => "EFT",
        MembershipPaymentMethod.Square => "Square",
        MembershipPaymentMethod.BankTransfer => "Bank transfer",
        MembershipPaymentMethod.Other => "Other",
        null => "—",
        _ => method.ToString() ?? "—",
    };

    public static string FormatTimelineEventType(MembershipTimelineEventType eventType) => eventType switch
    {
        MembershipTimelineEventType.ApplicationCreated => "Application created",
        MembershipTimelineEventType.Edited => "Edited",
        MembershipTimelineEventType.Submitted => "Submitted",
        MembershipTimelineEventType.Approved => "Approved",
        MembershipTimelineEventType.Rejected => "Rejected",
        MembershipTimelineEventType.MarkedPaid => "Marked paid",
        MembershipTimelineEventType.MembershipActivated => "Membership activated",
        MembershipTimelineEventType.CardIssued => "Card issued",
        MembershipTimelineEventType.AddedToRegister => "Added to register",
        _ => eventType.ToString(),
    };
}
