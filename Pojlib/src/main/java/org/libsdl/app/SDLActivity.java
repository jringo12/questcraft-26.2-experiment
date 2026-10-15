package org.libsdl.app;

import android.app.Activity;
import android.util.DisplayMetrics;
import android.util.Log;
import android.view.Display;
import android.view.Surface;
import android.view.WindowManager;

/**
 * Minimal SDL3 Android bridge for QuestCraft.
 *
 * Minecraft 26.3 replaces GLFW with SDL3. SDL3's Android video driver
 * expects a classic SDLActivity: SDL's native code resolves a set of static
 * Java callbacks on {@code org.libsdl.app.SDLActivity} (via
 * {@code nativeSetupJNI}) and needs the Activity + Surface handed to it,
 * plus {@code SDL_SetMainReady()} called before {@code SDL_Init} (SDL3 is
 * built with {@code SDL_MAIN_NEEDED} on Android, so {@code SDL_Init} fails
 * with "did you include SDL_main.h ..." when the flag is unset).
 *
 * QuestCraft's activity is Unity's {@code UnityPlayerActivity}, not an
 * SDLActivity, so this class is a plain bridge (NOT an Activity): it
 * exposes the exact static callbacks SDL's {@code nativeSetupJNI} looks
 * up, backed by the Unity activity's Minecraft {@link Surface}. The
 * game (LWJGL) dlopens the same {@code libSDL3.so} instance this bridge
 * loads, so the flag / activity / surface set here are what the game sees.
 *
 * Deliberately minimal (bridge-only, no native hook layer): the upstream
 * launchers' extra hook layers (window reuse, ES-profile forcing, EGL
 * normalisation, Vulkan-loader sharing, IME bridging, mouse-focus
 * fallback) exist for RenderPearl / OpenGL-ES / soft-keyboard paths
 * QuestCraft does not use -- 26.3 on QuestCraft renders via Vulkan and
 * takes input via Vivecraft/OpenXR + the GLFW CallbackBridge. This
 * bridge only makes {@code SDL_Init} / {@code SDL_CreateWindow} /
 * {@code SDL_Vulkan_CreateSurface} succeed against the Quest's real
 * {@code ANativeWindow}.
 *
 * Inert for 26.2 (GLFW): nothing here runs unless
 * {@code JREUtils.launchJavaVM} calls {@link #setup} for an SDL version.
 * All surface-forwarding calls are no-ops until {@code setup} has run.
 */
public final class SDLActivity {
    private static final String TAG = "QuestCraft-SDL";

    // State set by setup(); volatile because SDL reads it from the game thread.
    private static volatile Activity sActivity;
    private static volatile Surface sSurface;
    private static volatile boolean sSetupDone;
    private static volatile boolean sSurfaceCreatedSignaled;

    private SDLActivity() {}

    /**
     * Eager ART-side SDL setup. Must run BEFORE the game JVM starts, on an
     * ART thread, and only for SDL versions (26.3+). Idempotent: repeat
     * calls just refresh the Surface.
     *
     * @param activity the Unity activity hosting the game
     * @param surface  the Minecraft SurfaceView surface (may be null; SDL
     *                 will fail window creation until a valid one arrives
     *                 via {@link #updateSurface})
     * @return true if the bridge is ready (SDL loaded, JNI set up, main ready)
     */
    public static synchronized boolean setup(Activity activity, Surface surface) {
        if (activity == null) {
            Log.w(TAG, "setup: null activity, bridge not started");
            return false;
        }
        sActivity = activity;
        if (surface != null && surface.isValid()) {
            sSurface = surface;
        }
        if (sSetupDone) {
            // Already set up (e.g. game relaunch): refresh the surface signal.
            if (sSurface != null && sSurface.isValid() && !sSurfaceCreatedSignaled) {
                try {
                    onNativeSurfaceCreated();
                    sSurfaceCreatedSignaled = true;
                } catch (Throwable t) {
                    Log.w(TAG, "setup: onNativeSurfaceCreated failed", t);
                }
            }
            return true;
        }
        try {
            // Loads the APK's libSDL3.so and runs SDL's JNI_OnLoad on the ART
            // VM (saves mJavaVM = ART, registers the native methods below).
            // The game later dlopens the same instance by soname, so it
            // inherits this setup instead of loading a second copy.
            System.loadLibrary("SDL3");
            // Tiny glue lib that calls SDL_SetMainReady() (not a JNI method).
            System.loadLibrary("sdlbridge");
        } catch (Throwable t) {
            Log.e(TAG, "setup: failed to load SDL3/sdlbridge", t);
            return false;
        }
        try {
            nativeSetupJNI();
        } catch (Throwable t) {
            Log.e(TAG, "setup: nativeSetupJNI failed", t);
            return false;
        }
        try {
            if (!nativeSetMainReady()) {
                Log.e(TAG, "setup: SDL_SetMainReady failed; SDL_Init will fail");
                return false;
            }
        } catch (Throwable t) {
            Log.e(TAG, "setup: nativeSetMainReady failed", t);
            return false;
        }
        setScreenResolution(activity);
        if (sSurface != null && sSurface.isValid()) {
            try {
                onNativeSurfaceCreated();
                sSurfaceCreatedSignaled = true;
            } catch (Throwable t) {
                Log.w(TAG, "setup: onNativeSurfaceCreated failed", t);
            }
        }
        sSetupDone = true;
        Log.i(TAG, "setup: SDL bridge ready (surface " + (sSurface != null ? "present" : "null") + ")");
        return true;
    }

    /** Forward a new/changed Minecraft Surface to SDL. No-op until setup. */
    public static synchronized void updateSurface(Surface surface) {
        if (!sSetupDone) return;
        if (surface == null || !surface.isValid()) {
            surfaceDestroyed();
            return;
        }
        sSurface = surface;
        try {
            if (!sSurfaceCreatedSignaled) {
                onNativeSurfaceCreated();
                sSurfaceCreatedSignaled = true;
            } else {
                onNativeSurfaceChanged();
                onNativeResize();
            }
        } catch (Throwable t) {
            Log.w(TAG, "updateSurface: SDL surface signal failed", t);
        }
    }

    /** Forward Surface destruction to SDL. No-op until setup. */
    public static synchronized void surfaceDestroyed() {
        if (!sSetupDone) return;
        sSurface = null;
        if (sSurfaceCreatedSignaled) {
            sSurfaceCreatedSignaled = false;
            try {
                onNativeSurfaceDestroyed();
            } catch (Throwable t) {
                Log.w(TAG, "surfaceDestroyed: SDL signal failed", t);
            }
        }
    }

    /** Report the display size to SDL (drives Android_VideoInit's mode). */
    private static void setScreenResolution(Activity activity) {
        try {
            WindowManager wm = activity.getWindowManager();
            Display display = wm != null ? wm.getDefaultDisplay() : null;
            DisplayMetrics dm = new DisplayMetrics();
            if (display != null) {
                display.getRealMetrics(dm);
            } else {
                dm.setTo(activity.getResources().getDisplayMetrics());
            }
            int deviceW = dm.widthPixels;
            int deviceH = dm.heightPixels;
            float density = dm.density;
            float rate = display != null ? display.getRefreshRate() : 60.0f;
            // SurfaceView is MATCH_PARENT, so surface == device size.
            nativeSetScreenResolution(deviceW, deviceH, deviceW, deviceH, density, rate);
        } catch (Throwable t) {
            Log.w(TAG, "setScreenResolution failed", t);
        }
    }

    // ------------------------------------------------------------------
    // Static callbacks SDL's nativeSetupJNI resolves on this class.
    // Only getContext() and getNativeSurface() do real work; the rest are
    // safe stubs (SDL logs a warning if any is missing, so all 31 are
    // present). QuestCraft drives input/clipboard/orientation itself,
    // so SDL's IME / cursor / messagebox / file-dialog callbacks are inert.
    // ------------------------------------------------------------------

    public static Activity getContext() {
        return sActivity;
    }

    public static Surface getNativeSurface() {
        return sSurface;
    }

    public static String clipboardGetText() {
        return "";
    }

    public static boolean clipboardHasText() {
        return false;
    }

    public static void clipboardSetText(String text) {
    }

    public static int createCustomCursor(int[] colors, int width, int height, int hotSpotX, int hotSpotY) {
        return 0;
    }

    public static void destroyCustomCursor(int cursorID) {
    }

    public static boolean getManifestEnvironmentVariables() {
        return false;
    }

    public static void initTouch() {
    }

    public static boolean isAndroidTV() {
        return false;
    }

    public static boolean isChromebook() {
        return false;
    }

    public static boolean isDeXMode() {
        return false;
    }

    public static boolean isTablet() {
        return false;
    }

    public static void manualBackButton() {
    }

    public static void minimizeWindow() {
    }

    public static boolean openURL(String url) {
        return false;
    }

    public static void requestPermission(String permission, int requestCode) {
    }

    public static boolean showToast(String message, int duration, int gravity, int xOffset, int yOffset) {
        return false;
    }

    public static boolean sendMessage(int command, int param) {
        return false;
    }

    public static boolean setActivityTitle(String title) {
        return false;
    }

    public static boolean setCustomCursor(int cursorID) {
        return false;
    }

    public static void setOrientation(int w, int h, boolean resizable, String hint) {
    }

    public static boolean setRelativeMouseEnabled(boolean enabled) {
        return false;
    }

    public static boolean setSystemCursor(int cursorID) {
        return false;
    }

    public static void setWindowStyle(boolean fullscreen) {
    }

    public static boolean shouldMinimizeOnFocusLoss() {
        return false;
    }

    public static boolean showTextInput(int x, int y, int w, int h, int inputType) {
        return false;
    }

    public static boolean supportsRelativeMouse() {
        return false;
    }

    public static int openFileDescriptor(String path, String mode) {
        return -1;
    }

    public static boolean showFileDialog(String[] filters, boolean allowMultiple, boolean isSave, int requestCode) {
        return false;
    }

    public static String getPreferredLocales() {
        return "";
    }

    // ------------------------------------------------------------------
    // Native methods implemented by libSDL3.so (registered in its
    // JNI_OnLoad when System.loadLibrary("SDL3") runs above).
    //
    // CRITICAL (6.0.5-RC4): SDL's JNI_OnLoad calls RegisterNatives with
    // the FULL SDLActivity table (44 methods). RegisterNatives FAILS
    // (leaving a pending NoSuchMethodError that aborts the launcher
    // during bridge setup) if ANY listed method is missing from this
    // class. RC3 declared only 6 of them, so JNI_OnLoad failed and the
    // process died silently before the game JVM started. EVERY method
    // in SDL's SDLActivity_tab must be declared here with the EXACT
    // name + signature, even the ones QuestCraft never calls (they are
    // inert stubs: SDL only invokes them if the corresponding subsystem
    // is set up, and the bridge never sets up input/audio/controller).
    // The same applies to the companion stub classes
    // SDLInputConnection / SDLAudioManager / SDLControllerManager /
    // HIDDeviceManager: JNI_OnLoad FindClass'es all five classes, so
    // all five must exist with their full native tables.
    // ------------------------------------------------------------------

    private static native String nativeGetVersion();

    private static native void nativeSetupJNI();

    private static native void nativeInitMainThread();

    private static native void nativeCleanupMainThread();

    private static native int nativeRunMain(String library, String function, Object arguments);

    private static native void onNativeDropFile(String filename);

    private static native void nativeSetScreenResolution(
            int surfaceWidth, int surfaceHeight,
            int deviceWidth, int deviceHeight,
            float density, float rate);

    private static native void onNativeResize();

    private static native void onNativeSurfaceCreated();

    private static native void onNativeSurfaceChanged();

    private static native void onNativeSurfaceDestroyed();

    private static native void onNativeScreenKeyboardShown();

    private static native void onNativeScreenKeyboardHidden();

    private static native void onNativeKeyDown(int keycode);

    private static native void onNativeKeyUp(int keycode);

    private static native boolean onNativeSoftReturnKey();

    private static native void onNativeKeyboardFocusLost();

    private static native void onNativeTouch(int touchDevId, int pointerFingerId,
            int action, float x, float y, float p);

    private static native void onNativePinchStart();

    private static native void onNativePinchUpdate(float scale);

    private static native void onNativePinchEnd();

    private static native void onNativeMouse(int button, int action, float x, float y, boolean relative);

    private static native void onNativePen(int penId, int device_type, int button,
            int action, float x, float y, float p);

    private static native void onNativeAccel(float x, float y, float z);

    private static native void onNativeClipboardChanged();

    private static native void nativeLowMemory();

    private static native void onNativeLocaleChanged();

    private static native void onNativeDarkModeChanged(boolean enabled);

    private static native void nativeSendQuit();

    private static native void nativeQuit();

    private static native void nativePause();

    private static native void nativeResume();

    private static native void nativeFocusChanged(boolean hasFocus);

    private static native String nativeGetHint(String name);

    private static native boolean nativeGetHintBoolean(String name, boolean default_value);

    private static native void nativeSetenv(String name, String value);

    private static native void nativeSetNaturalOrientation(int orientation);

    private static native void onNativeRotationChanged(int rotation);

    private static native void onNativeInsetsChanged(int left, int right, int top, int bottom);

    private static native void nativeAddTouch(int touchId, String name);

    private static native void nativePermissionResult(int requestCode, boolean result);

    private static native boolean nativeAllowRecreateActivity();

    private static native int nativeCheckSDLThreadCounter();

    private static native void onNativeFileDialog(int requestCode, String[] filelist, int filter);

    // ------------------------------------------------------------------
    // Native method implemented by libsdlbridge.so (QuestCraft glue):
    // dlsyms SDL_SetMainReady() from the already-loaded libSDL3.so and
    // calls it. Not part of SDL's JNI table, so it resolves by symbol.
    // ------------------------------------------------------------------

    private static native boolean nativeSetMainReady();
}
