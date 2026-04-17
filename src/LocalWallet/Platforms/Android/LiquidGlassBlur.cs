using Android.OS;
using Android.Views;
using LocalWallet.Controls;
using Microsoft.Maui.Handlers;

namespace LocalWallet.Platforms.Android;

/// <summary>
/// Android 12+ real-time backdrop blur for <see cref="LiquidGlassTabBar"/>.
/// Attaches a <see cref="global::Android.Graphics.RenderEffect"/> to the
/// backdrop host so the native view samples whatever is rendered behind it.
/// On Android 11 and below we leave the static tint in place.
/// </summary>
public static class LiquidGlassBlur
{
    public static void Configure()
    {
        BorderHandler.Mapper.AppendToMapping("LiquidGlassBlur", (handler, view) =>
        {
            if (view.Parent is Grid parentGrid &&
                parentGrid.Parent is Border root &&
                root.Parent is LiquidGlassTabBar bar &&
                ReferenceEquals(view, bar.BlurBackdropHost))
            {
                ApplyRealtimeBlur(handler.PlatformView);
            }
        });
    }

    private static void ApplyRealtimeBlur(global::Android.Views.View? nativeView)
    {
        if (nativeView is null) return;
        if (Build.VERSION.SdkInt < BuildVersionCodes.S) return; // Android 12 = API 31

        try
        {
            var effect = global::Android.Graphics.RenderEffect.CreateBlurEffect(
                32f, 32f,
                global::Android.Graphics.Shader.TileMode.Clamp);
            nativeView.SetRenderEffect(effect);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LiquidGlassBlur] failed: {ex}");
        }
    }
}
