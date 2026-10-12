using System;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace StickmanJumper.Maps
{
    /// <summary>
    /// Обмен текстом с другими приложениями: системное «Поделиться» (Telegram, WhatsApp…) и буфер обмена.
    /// На Android — через его API (буфер трогаем в потоке интерфейса Android), в редакторе — обычный буфер Unity.
    /// </summary>
    public static class DeviceShare
    {
        public static void Copy(string text)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                AndroidApplication.InvokeOnUIThread(() =>
                {
                    try
                    {
                        using var manager = Clipboard();
                        using var clipData = new AndroidJavaClass("android.content.ClipData");
                        using var clip = clipData.CallStatic<AndroidJavaObject>("newPlainText", "map", text);
                        manager.Call("setPrimaryClip", clip);
                    }
                    catch (Exception e) { Debug.LogWarning($"[Share] copy: {e.Message}"); }
                });
                return;
            }
            catch (Exception e) { Debug.LogWarning($"[Share] copy: {e.Message}"); }
#endif
            GUIUtility.systemCopyBuffer = text;
        }

        /// <summary>Текст из буфера обмена (на Android ответ приходит чуть позже, в главном потоке игры).</summary>
        public static void Paste(Action<string> done)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                AndroidApplication.InvokeOnUIThread(() =>
                {
                    string text = string.Empty;
                    try
                    {
                        using var manager = Clipboard();
                        using var clip = manager.Call<AndroidJavaObject>("getPrimaryClip");
                        if (clip != null && clip.Call<int>("getItemCount") > 0)
                        {
                            using var item = clip.Call<AndroidJavaObject>("getItemAt", 0);
                            using var chars = item.Call<AndroidJavaObject>("coerceToText", AndroidApplication.currentActivity);
                            text = chars != null ? chars.Call<string>("toString") : string.Empty;
                        }
                    }
                    catch (Exception e) { Debug.LogWarning($"[Share] paste: {e.Message}"); }
                    AndroidApplication.InvokeOnUnityMainThread(() => done(text ?? string.Empty));
                });
                return;
            }
            catch (Exception e) { Debug.LogWarning($"[Share] paste: {e.Message}"); }
#endif
            done(GUIUtility.systemCopyBuffer ?? string.Empty);
        }

        /// <summary>Системное окно «Поделиться» с текстом. Где его нет (редактор) — текст просто копируется.</summary>
        public static void Send(string subject, string text)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var intentClass = new AndroidJavaClass("android.content.Intent");
                using var intent = new AndroidJavaObject("android.content.Intent");
                intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND")).Dispose();
                intent.Call<AndroidJavaObject>("setType", "text/plain").Dispose();
                intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_SUBJECT"), subject).Dispose();
                intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), text).Dispose();
                using var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, subject);
                AndroidApplication.currentActivity.Call("startActivity", chooser);
                return;
            }
            catch (Exception e) { Debug.LogWarning($"[Share] send: {e.Message}"); }
#endif
            Copy(text);
        }

        /// <summary>Есть ли системное «Поделиться» (иначе кнопка «отправить» только копирует).</summary>
        public static bool CanSend =>
#if UNITY_ANDROID && !UNITY_EDITOR
            true;
#else
            false;
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject Clipboard() => AndroidApplication.currentActivity.Call<AndroidJavaObject>("getSystemService", "clipboard");
#endif
    }
}
