# App-level R8/ProGuard rules for release builds (isMinifyEnabled + isShrinkResources).
#
# The libraries in use ship their own consumer ProGuard rules, which R8 merges automatically —
# Firebase / GMS, androidx.credentials (+ play-services-auth), androidx.biometric,
# kotlinx-coroutines, file_picker, mobile_scanner. Do not duplicate those here.
#
# Flutter's engine references Play Core split-install classes that this app does not depend on. Under
# R8 full mode that surfaces as "missing class com.google.android.play.core.*" and fails the release
# build. The referenced code paths (deferred components) are never taken here, so suppress them.
-dontwarn com.google.android.play.core.**
-keep class com.google.android.play.core.** { *; }
