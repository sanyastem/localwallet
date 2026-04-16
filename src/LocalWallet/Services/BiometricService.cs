using Plugin.Fingerprint;
using Plugin.Fingerprint.Abstractions;

namespace LocalWallet.Services;

public interface IBiometricService
{
    Task<bool> IsAvailableAsync();
    Task<bool> AuthenticateAsync(string reason);
}

public class BiometricService : IBiometricService
{
    public async Task<bool> IsAvailableAsync()
    {
        var available = await CrossFingerprint.Current.IsAvailableAsync(allowAlternativeAuthentication: true);
        return available;
    }

    public async Task<bool> AuthenticateAsync(string reason)
    {
        var request = new AuthenticationRequestConfiguration("LocalWallet", reason)
        {
            AllowAlternativeAuthentication = true,
            CancelTitle = "Отмена"
        };
        var result = await CrossFingerprint.Current.AuthenticateAsync(request);
        return result.Authenticated;
    }
}
