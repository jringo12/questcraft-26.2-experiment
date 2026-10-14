LOCAL_PATH := $(call my-dir)
HERE_PATH := $(LOCAL_PATH)

# include $(HERE_PATH)/crash_dump/libbase/Android.mk
# include $(HERE_PATH)/crash_dump/libbacktrace/Android.mk
# include $(HERE_PATH)/crash_dump/debuggerd/Android.mk

LOCAL_PATH := $(HERE_PATH)

include $(CLEAR_VARS)
LOCAL_MODULE := openxr_loader
LOCAL_SRC_FILES := libopenxr_loader.so
include $(PREBUILT_SHARED_LIBRARY)

# SDL3 (Android bionic build, built from SDL 3.4.14 source in RC1).
# The APK ships this copy so the ART-side SDL bridge can load it via
# System.loadLibrary("SDL3"); the game then dlopens the same instance
# by soname. Prebuilt, like openxr_loader above.
include $(CLEAR_VARS)
LOCAL_MODULE := SDL3
LOCAL_SRC_FILES := libSDL3.so
include $(PREBUILT_SHARED_LIBRARY)

# QuestCraft SDL bridge glue: one JNI method
# (SDLActivity.nativeSetMainReady) that calls SDL_SetMainReady() in the
# already-loaded libSDL3.so. No SDL calls are intercepted.
include $(CLEAR_VARS)
LOCAL_MODULE := sdlbridge
LOCAL_SRC_FILES := sdl_bridge.c
LOCAL_LDLIBS := -ldl -llog
include $(BUILD_SHARED_LIBRARY)

include $(CLEAR_VARS)
# Link GLESv2 for test
LOCAL_LDLIBS := -ldl -llog -landroid -lGLESv3 -lEGL
LOCAL_CFLAGS := -DXR_USE_PLATFORM_ANDROID -DXR_USE_GRAPHICS_API_OPENGL_ES -DXR_USE_GRAPHICS_API_VULKAN
# -lGLESv2
LOCAL_MODULE := pojavexec
# LOCAL_CFLAGS += -DDEBUG
# -DGLES_TEST
LOCAL_SRC_FILES := \
    egl_bridge.c \
    utils.c \
    environ/environ.c \
    input_bridge_v3.c
include $(BUILD_SHARED_LIBRARY)

include $(CLEAR_VARS)
LOCAL_LDLIBS := -llog -landroid -lGLESv3 -lvulkan -lEGL
LOCAL_CFLAGS := -DXR_USE_PLATFORM_ANDROID -DXR_USE_GRAPHICS_API_OPENGL_ES -DXR_USE_GRAPHICS_API_VULKAN
LOCAL_SHARED_LIBRARIES := pojavexec openxr_loader
LOCAL_MODULE := vloader
LOCAL_SRC_FILES := \
            vloader.cpp
include $(BUILD_SHARED_LIBRARY)

include $(CLEAR_VARS)
LOCAL_MODULE := istdio
LOCAL_SRC_FILES := \
    stdio_is.c
include $(BUILD_SHARED_LIBRARY)

include $(CLEAR_VARS)
LOCAL_MODULE := pojavexec_awt
LOCAL_SRC_FILES := \
    awt_bridge.c
include $(BUILD_SHARED_LIBRARY)

include $(CLEAR_VARS)
LOCAL_MODULE := jrelauncher
LOCAL_SHARED_LIBRARIES := pojavexec
LOCAL_LDLIBS := -llog -landroid
LOCAL_SRC_FILES := \
    jre_launcher.c
include $(BUILD_SHARED_LIBRARY)

# Helper to get current thread
# include $(CLEAR_VARS)
# LOCAL_MODULE := thread64helper
# LOCAL_SRC_FILES := thread_helper.cpp
# include $(BUILD_SHARED_LIBRARY)

# fake lib for linker
include $(CLEAR_VARS)
LOCAL_MODULE := awt_headless
include $(BUILD_SHARED_LIBRARY)

# libawt_xawt without X11, used to get Caciocavallo working
LOCAL_PATH := $(HERE_PATH)/awt_xawt
include $(CLEAR_VARS)
LOCAL_MODULE := awt_xawt
# LOCAL_CFLAGS += -DHEADLESS
LOCAL_EXPORT_C_INCLUDES := $(LOCAL_PATH)
LOCAL_SHARED_LIBRARIES := awt_headless
LOCAL_SRC_FILES := xawt_fake.c
include $(BUILD_SHARED_LIBRARY)

# delete fake libs after linked
$(info $(shell (rm $(HERE_PATH)/../jniLibs/*/libawt_headless.so)))

