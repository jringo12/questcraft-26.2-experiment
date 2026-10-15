package org.libsdl.app;

/**
 * Stub for SDL3's {@code org.libsdl.app.SDLAudioManager}.
 *
 * SDL's {@code JNI_OnLoad} (libSDL3.so) calls {@code FindClass} for this
 * class and {@code RegisterNatives} with its 3-method table. If the class
 * (or any listed method) is missing, {@code RegisterNatives} fails and
 * leaves a pending exception that aborts the launcher during bridge
 * setup (6.0.5-RC3 silent-death bug). This stub exists ONLY so
 * {@code JNI_OnLoad} completes cleanly: QuestCraft never calls
 * {@code nativeSetupJNI} on this class (game audio goes through LWJGL
 * OpenAL, not SDL audio), so these methods never run. The declarations
 * must match SDL's {@code SDLAudioManager_tab} exactly (name + JNI
 * signature). If a future build routes game audio through SDL, this
 * stub must grow into a functional manager instead.
 */
public final class SDLAudioManager {
    private SDLAudioManager() {}

    static native void nativeSetupJNI();

    static native void nativeAddAudioDevice(boolean recording, String name, int deviceId);

    static native void nativeRemoveAudioDevice(boolean recording, int deviceId);
}
