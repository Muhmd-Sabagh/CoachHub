namespace CoachHub.Application.Clients;

public interface IClientCodeGenerator
{
    string GenerateClientCode();
    string GenerateFormCode();
}