// QuestCraft SDL bridge glue (libsdlbridge.so).
//
// One JNI method: SDLActivity.nativeSetMainReady(). SDL3 on Android is
// built with SDL_MAIN_NEEDED, so SDL_Init() fails ("did you include
// SDL_main.h ...") unless SDL_SetMainReady() has been called. Minecraft
// has no SDL_main, so the launcher calls it eagerly on the ART side
// before the game JVM starts. SDL_SetMainReady() is a plain C export in
// libSDL3.so (not a JNI method), so this glue dlsyms it from the
// already-loaded instance (RTLD_NOLOAD: fail rather than load a second
// copy) and calls it.
//
// No hooking: this library does not intercept any SDL call. The SDL
// Java bridge (org.libsdl.app.SDLActivity) supplies the Activity/Surface
// SDL's Android driver needs; LWJGL then uses that same libSDL3.so.

#include <dlfcn.h>
#include <jni.h>
#include <android/log.h>

#define TAG "QuestCraft-SDL"

typedef void (*SDL_SetMainReady_t)(void);

JNIEXPORT jboolean JNICALL
Java_org_libsdl_app_SDLActivity_nativeSetMainReady(JNIEnv *env, jclass clazz) {
    (void) env;
    (void) clazz;
    // libSDL3.so must already be loaded by the ART side (System.loadLibrary
    // in SDLActivity.setup). RTLD_NOLOAD returns that handle; if SDL3 is
    // not loaded yet, this is a caller-ordering bug -- do not load a fresh
    // copy, or the game would see a different SDL instance.
    void *handle = dlopen("libSDL3.so", RTLD_NOLOAD | RTLD_LOCAL);
    if (handle == NULL) {
        __android_log_print(ANDROID_LOG_ERROR, TAG,
                            "nativeSetMainReady: libSDL3.so not loaded yet");
        return JNI_FALSE;
    }
    SDL_SetMainReady_t setMainReady =
        (SDL_SetMainReady_t) dlsym(handle, "SDL_SetMainReady");
    if (setMainReady == NULL) {
        __android_log_print(ANDROID_LOG_ERROR, TAG,
                            "nativeSetMainReady: SDL_SetMainReady not found");
        return JNI_FALSE;
    }
    setMainReady();
    __android_log_print(ANDROID_LOG_INFO, TAG,
                        "nativeSetMainReady: SDL_MainIsReady set");
    return JNI_TRUE;
}
