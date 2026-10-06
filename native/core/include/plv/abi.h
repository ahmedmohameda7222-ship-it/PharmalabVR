#ifndef PLV_ABI_H
#define PLV_ABI_H

#include <stdint.h>

#if defined(_WIN32) && defined(PLV_BUILD_SHARED)
#define PLV_API __declspec(dllexport)
#elif defined(_WIN32)
#define PLV_API __declspec(dllimport)
#else
#define PLV_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

enum plv_status {
    PLV_OK = 0,
    PLV_BUFFER_TOO_SMALL = 1,
    PLV_NO_EVENT = 2,
    PLV_INVALID_ARGUMENT = 3,
    PLV_INVALID_HANDLE = 4,
    PLV_UNSUPPORTED_VERSION = 5,
    PLV_BUSY = 6,
    PLV_INTERNAL_ERROR = 7
};

PLV_API uint32_t plv_abi_version(void);
PLV_API int32_t plv_create(const char* config, uint32_t size, uint64_t* handle);
PLV_API int32_t plv_destroy(uint64_t handle);
PLV_API int32_t plv_submit(uint64_t handle, const char* command, uint32_t size);
PLV_API int32_t plv_input_batch(uint64_t handle, const char* samples, uint32_t size);
PLV_API int32_t plv_step(uint64_t handle, double delta_s, uint64_t monotonic_now_ns);
PLV_API int32_t plv_snapshot(uint64_t handle, char* out, uint32_t capacity, uint32_t* required);
PLV_API int32_t plv_poll(uint64_t handle, char* out, uint32_t capacity, uint32_t* required);
PLV_API int32_t plv_export(uint64_t handle, char* out, uint32_t capacity, uint32_t* required);
PLV_API int32_t plv_import(const char* json, uint32_t size, uint64_t* new_handle);

#ifdef __cplusplus
}
#endif

#endif
