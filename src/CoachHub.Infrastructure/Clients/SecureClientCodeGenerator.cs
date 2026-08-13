using System.Security.Cryptography;
using CoachHub.Application.Clients;

namespace CoachHub.Infrastructure.Clients;

public sealed class SecureClientCodeGenerator : IClientCodeGenerator
{
    public string GenerateClientCode() => Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
    public string GenerateFormCode() => Convert.ToHexString(RandomNumberGenerator.GetBytes(5));
}