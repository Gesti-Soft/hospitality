namespace GestiSoft.Application.Exceptions;

public class ConflictException(string userMessage) : DomainException(userMessage, statusCode: 409);
