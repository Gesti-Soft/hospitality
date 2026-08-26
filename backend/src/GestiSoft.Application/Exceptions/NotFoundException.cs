namespace GestiSoft.Application.Exceptions;

public class NotFoundException(string userMessage) : DomainException(userMessage, statusCode: 404);
