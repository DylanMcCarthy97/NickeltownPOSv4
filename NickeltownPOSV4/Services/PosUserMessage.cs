using System;
using System.Net.Http;

namespace NickeltownPOSV4.Services;

/// <summary>Maps exceptions to operator-safe status text (raw details go to logs).</summary>
public static class PosUserMessage
{
    public static string FromException(Exception ex, string fallback = "Something went wrong. Try again.")
    {
        return ex switch
        {
            OperationCanceledException => "Cancelled.",
            UnauthorizedAccessException => "You do not have permission for that action.",
            InvalidOperationException ioe when !string.IsNullOrWhiteSpace(ioe.Message) => ioe.Message,
            ArgumentException ae when !string.IsNullOrWhiteSpace(ae.Message) => ae.Message,
            HttpRequestException => "Network error. Check your connection and try again.",
            TimeoutException => "Timed out. Try again.",
            _ => fallback,
        };
    }
}
