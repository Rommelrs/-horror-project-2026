ULTRA HEAVY ASSETS - Clone Blockers
====================================
Measured 2026-04-15. These are the folders most likely to cause
clone failures on slow/unstable connections. Ordered heaviest first.

TIER 1 - ULTRA HEAVY (>500 MB) — clone these LAST, one at a time
-----------------------------------------------------------------
  1.2 GB   Assets/Models          <-- HEAVIEST, split alone
  868 MB   Assets/Knife           <-- clone alone
  752 MB   Assets/UpdatedEnvironment6   <-- caused Stage 6 failure likely
  601 MB   Assets/Prefab          <-- clone alone

TIER 2 - HEAVY (250-500 MB) — clone 1-2 per stage max
-----------------------------------------------------
  434 MB   Assets/Model 1
  396 MB   Assets/UpdatedEnvironment5
  376 MB   Assets/UpdatedEnvironment2
  333 MB   Assets/Enviro - Sky and Weather
  318 MB   Assets/RoadAsset
  308 MB   Assets/UpdatedEnvironment3
  255 MB   Assets/AllSkyFree
  253 MB   Assets/UpdatedEnvironment4

RECOMMENDED REVISED STAGE PLAN FOR SLOW CONNECTIONS
====================================================
Replace batched Stage 5-7 with one-folder-per-stage:

  git sparse-checkout add "Assets/AllSkyFree"
  git sparse-checkout add "Assets/UpdatedEnvironment4"
  git sparse-checkout add "Assets/UpdatedEnvironment3"
  git sparse-checkout add "Assets/RoadAsset"
  git sparse-checkout add "Assets/Enviro - Sky and Weather"
  git sparse-checkout add "Assets/UpdatedEnvironment2"
  git sparse-checkout add "Assets/UpdatedEnvironment5"
  git sparse-checkout add "Assets/Model 1"
  git sparse-checkout add "Assets/Prefab"
  git sparse-checkout add "Assets/UpdatedEnvironment6"
  git sparse-checkout add "Assets/Knife"
  git sparse-checkout add "Assets/Models"

If any single "add" fails, re-run just that line. Each command
resumes and only fetches the missing blobs.

GIT RESILIENCE TIPS (run BEFORE cloning)
========================================
  git config --global http.postBuffer 1048576000
  git config --global http.lowSpeedLimit 1000
  git config --global http.lowSpeedTime 300
  git config --global core.compression 0
