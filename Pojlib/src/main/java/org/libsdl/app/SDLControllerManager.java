package org.libsdl.app;

/**
 * Stub for SDL3's {@code org.libsdl.app.SDLControllerManager}.
 *
 * SDL's {@code JNI_OnLoad} (libSDL3.so) calls {@code FindClass} for this
 * class and {@code RegisterNatives} with its 10-method table. If the
 * class (or any listed method) is missing, {@code RegisterNatives} fails
 * and leaves a pending exception that aborts the launcher during bridge
 * setup (6.0.5-RC3 silent-death bug). This stub exists ONLY so
 * {@code JNI_OnLoad} completes cleanly: QuestCraft never calls
 * {@code nativeSetupJNI} on this class (controller input stays on
 * Vivecraft/OpenXR), so these methods never run. The declarations must
 * match SDL's {@code SDLControllerManager_tab} exactly (name + JNI
 * signature).
 */
public final class SDLControllerManager {
    private SDLControllerManager() {}

    static native void nativeSetupJNI();

    public static native boolean onNativePadDown(int device_id, int keycode, int scancode);

    public static native boolean onNativePadUp(int device_id, int keycode, int scancode);

    static native void onNativeJoy(int device_id, int axis, float value);

    static native void onNativeHat(int device_id, int hat_id, int x, int y);

    static native void onNativeJoySensor(int device_id, int sensor_type,
            long sensor_timestamp, float x, float y, float z);

    static native void nativeAddJoystick(int device_id, String device_name, String device_desc,
            int vendor_id, int product_id, int button_mask, int naxes, int axis_mask, int nhats,
            boolean can_rumble, boolean has_rgb_led, boolean has_accelerometer, boolean has_gyroscope);

    static native void nativeRemoveJoystick(int device_id);

    static native void nativeAddHaptic(int device_id, String device_name);

    static native void nativeRemoveHaptic(int device_id);
}
