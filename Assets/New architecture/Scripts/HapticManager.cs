using UnityEngine;

public static class HapticManager
{
    /// <summary>
    /// Vibración muy corta y sutil (ideal para botones o pedir pistas).
    /// </summary>
    public static void LightVibration()
    {
        Vibrate(20); // 20 milisegundos
    }

    /// <summary>
    /// Vibración más larga y fuerte (ideal para errores o perder vidas).
    /// </summary>
    public static void HeavyVibration()
    {
        Vibrate(80); // 80 milisegundos
    }

    /// <summary>
    /// Vibración intermedia para celebrar rachas.
    /// </summary>
    public static void SuccessVibration()
    {
        Vibrate(40); // 40 milisegundos
    }

    // Método interno que habla directamente con el hardware
    private static void Vibrate(long milliseconds)
    {
        // Solo intentamos vibrar si estamos compilados en un celular real
        if (Application.isMobilePlatform)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                // Código nativo de Android para controlar los milisegundos exactos
                AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                AndroidJavaObject vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                vibrator.Call("vibrate", milliseconds);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Error en la vibración: " + e.Message);
            }
#elif UNITY_IOS && !UNITY_EDITOR
            // En iOS el control de milisegundos requiere plugins de pago, 
            // así que usamos la vibración nativa del sistema como respaldo.
            Handheld.Vibrate(); 
#endif
        }
    }
}