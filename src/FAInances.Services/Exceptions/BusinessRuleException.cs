namespace FAInances.Services.Exceptions;

/// <summary>Thrown when input violates a business rule; the message is safe to show to the user.</summary>
public class BusinessRuleException(string message) : Exception(message);
