using Backend.AuthenticationSystem;
using Microsoft.Extensions.DependencyInjection;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace Backend;

public class GlobalModuleConfig : ICloudCodeSetup
{
    public void Setup(ICloudCodeConfig config)
    {
        // Registrar el GameApiClient una sola vez
        config.Dependencies.AddSingleton(GameApiClient.Create());

        // Registrar tus módulos
        config.Dependencies.AddSingleton<SignInWithCredentialsModule>();
        config.Dependencies.AddSingleton<SignInIDTokenInFirebaseModule>();
        config.Dependencies.AddSingleton<SignInProviderInFirebaseModule>();
        config.Dependencies.AddSingleton<SignUpWithCredentialsModule>();
    }
}
