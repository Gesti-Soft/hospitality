namespace GestiSoft.Application.Exceptions;

/// <summary>Nome non "UnauthorizedException" per non confondersi con System.UnauthorizedAccessException.</summary>
public class UnauthorizedAppException(string userMessage) : DomainException(userMessage, statusCode: 401);
