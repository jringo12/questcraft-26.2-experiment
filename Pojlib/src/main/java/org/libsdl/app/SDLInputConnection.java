package org.libsdl.app;

/**
 * Stub for SDL3's {@code org.libsdl.app.SDLInputConnection}.
 *
 * SDL's {@code JNI_OnLoad} (libSDL3.so) calls {@code FindClass} for this
 * class and {@code RegisterNatives} with its 2-method table. If the class
 * (or any listed method) is missing, {@code RegisterNatives} fails and
 * leaves a pending exception that aborts the launcher during bridge
 * setup (6.0.5-RC3 silent-death bug). This stub exists ONLY so
 * {@code JNI_OnLoad} completes cleanly: QuestCraft never creates an
 * {@code SDLInputConnection} and never calls these methods (SDL input
 * stays on the GLFW CallbackBridge + Vivecraft/OpenXR controllers).
 * The declarations must match SDL's {@code SDLInputConnection_tab}
 * exactly (name + JNI signature).
 */
public final class SDLInputConnection {
    private SDLInputConnection() {}

    public static native void nativeCommitText(String text, int newCursorPosition);

    public static native void nativeGenerateScancodeForUnichar(char c);
}
