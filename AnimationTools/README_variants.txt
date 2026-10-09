khalid_retarget.py variants (load_variant overrides), all with REFFILE ref_dump_running.json, V=3.1/1.1, TWIST_FOLLOW .9, FWD_KEEP 1.0:
CalmFixed: KEYS=24 YS=.667 BOB_SCALE=.85 HIP_LIFT=.012 HEAD_LEVEL=1 LEAN_SCALE=.85 YAW_SCALE=.8 ELBOW_OUT=.65 HAND_IN=.8 HEAD_OFF=8 KICK_SCALE=.55 ARM_SWING=.55
Chase (subtly aggressive): KEYS=22 YS=.70 BOB_SCALE=.8 HIP_LIFT=0 HEAD_LEVEL=1 LEAN_SCALE=1.05 YAW_SCALE=.9 ELBOW_OUT=.55 HAND_IN=.7 HEAD_OFF=13 KICK_SCALE=.7 ARM_SWING=.75
ChaseBent (stance knees bent like SH2): same as Chase but HIP_LIFT=-0.08 (stance knee flex ~44 deg true, was ~16)
ChaseStride: ChaseBent + FA_SHIFT=0.15 (foot path shifted back vs pelvis: thigh range -27..+40 deg like the Mixamo ref; ChaseBent only reached ~0 so the trailing leg folded under him)
ChaseStrider: KEYS=25 YS=.80 BOB_SCALE=.95 HIP_LIFT=-.07 KICK_SCALE=.8 FA_SHIFT=.13 (rest as Chase); step 1.29 m, 144 spm, thigh ~-37..+42
ChaseSprint (for SprintSpeed 3.7): V=3.7/1.1 KEYS=22 YS=.85 BOB_SCALE=1.0 HIP_LIFT=-.06 KICK_SCALE=.85 FA_SHIFT=.13 LEAN_SCALE=1.1 ARM_SWING=.8 (rest as Chase); step 1.36 m, 164 spm, thigh -42..+42
ChaseNatural (speed 3.1): KEYS=25 YS=.80 BOB_SCALE=.95 HIP_LIFT=-.04 KICK_SCALE=.5 FA_SHIFT=.12 (rest as Chase). Swing knee peak ~78 deg (was ~103), knee ~27 deg at touchdown (was ~42)
ChaseReal (based on IRL Run.mov comparison): KEYS=25 YS=.80 BOB_SCALE=.95 HIP_LIFT=-.01 KICK_SCALE=.75 FA_SHIFT=.12 LAND_LOW=.6 (rest as Chase). midstance knee ~10, contact ~25, swing peak ~89, thigh -40..+38
ChaseStraight: ChaseReal + HIP_BUMP=.09 REACH=.995 LAND_LOW=.9 LAND_REACH=.8 (hips rise at foot strike, foot reaches landing spot earlier; leg ratio hip-ankle/leg length ~.985 at contact, was .93 which reads as ~45 deg bend). NOTE: knee bend look ~ ratio: ratio .93 ~ 45deg visually; rest standing ratio is .974
ChaseForward: ChaseStraight with FA_SHIFT=-0.03 (foot lands ~15 deg ahead of the hip at contact like the IRL runner, was ~0-5; push-off thigh ~-52)
ChaseForward2: FA_SHIFT=-0.08 (landing leg ~20 deg ahead of hip, ~15 at contact; push-off thigh ~-49)
ChaseNatural2 (arms+torso tuned to IRL Run.mov): ChaseForward2 + LEAN_SCALE=.6 HEAD_OFF=13 ARM_SWING=.58 FWD_KEEP=0 ARM_BIAS=16 ELBOW_FLAT=.75 ELBOW_TARGET=95 (rest as Chase). Lean 8-13 deg, upper arm -56..+27, elbow 84-97, hand z .97-1.25
ChaseForward3 (ForwardV2 params + FA_SHIFT=-0.22 DUTY_ADD=.05 REACH=.99): landing leg ~31 deg ahead of hip (was 20), contact ~25, push-off unchanged (~-52); arms/torso as in ChaseForward2. ChaseNatural2 (arm/torso variant) kept but unused.
ChaseForward4 (fixes the high-knee swing in Forward3): ForwardV2 arms/torso + FA_SHIFT=-.22 DUTY_ADD=.05 REACH=.99 LAND_LOW=.9 LAND_REACH=0 KICK_SCALE=.45 SWING_DELAY=.5. thigh -9..+43 (true), heel peak .34 (knee height, shank ~horizontal), knee flex peak 80, landing leg 30 deg ahead
ChaseStraight2: ChaseForward4 + STANCE_STRAIGHT=1.0 REACH=.98 (hips follow the reach limit while a foot is on the ground so the whole stance leg stays straight from landing to push-off; stance ratio mean .985, min .948)
ChaseStraight3: ChaseStraight2 + STRAIGHT_FROM=.05 DUTY_ADD=.17 FA_SHIFT=-.26 (both legs straight at the landing frame like the IRL inverted V: front ratio .97/.94, back .94/.96 at frames 5/17; needs front reach == back reach at landing; Footstep events at frames 5 and 17)
ChaseLift: ChaseStraight3 + KICK_SCALE=.55 SWING_LIFT=.06 SWING_LIFT_AT=.75 LIFT_UNDELAYED=.7 (heel lifts right after push-off instead of trailing low for ~5 frames; landing frames 5/17 still straight .94-.97; thigh peak 48)
ChaseLift2: ChaseLift + KICK_SCALE=.65 SWING_LIFT=.09 (heel peak .52, thigh 50, knee flex peak 94; landing frames still .92-.97)
ChaseLift3: ChaseLift2 + LATE_STRAIGHT=1 LATE_FROM=.7 LATE_RATIO=.97 (front leg reaches out straight in the last ~2 frames before touchdown: frames 4/16 ratio .95-.96; earlier LATE_FROM .5-.6 gave a straight-leg goose-step, thigh 66-74)
ChaseHeel: ChaseLift3 + HEEL_LAND=1 HEEL_LAND_FROM=.55 (late-swing foot turns toes-up to the reference heel-strike angle ~22 deg; foot bone flat = -17 deg pitch; frames 3/4 now +6/+20 deg vs flat, were -45/-12)
ChaseFlight: ChaseHeel + DUTY_ADD=.10 FA_SHIFT=-.22 FLIGHT_LIFT=.07 LIFT_UNDELAYED=.2 (flight phase: both feet ~14 cm off the ground in frames 3-4 and 15-17 with legs straight .94-.96; landings at frames 6/19, back leg ~.85 there; Footstep events at 6/25 and 19/25)
ChaseSmooth: ChaseFlight but FLIGHT_LIFT=0 STANCE_STRAIGHT=.5 FA_SHIFT=-.16 HZ_SMOOTH=40 HIP_BUMP=.05 (hip bob 14 cm sawtooth -> 3 cm smooth; still airborne frames 2-5/14-18; landing front .96-.97 at 27 deg; Footstep 6/25, 18/25)

=== 2026-10-09 rebuild (KEYED_LEGS) ===
Root causes found: (1) the IRL video runner moves ~2.1 m/s (pixel speed / body height) while the game sprint is 3.1-3.2 m/s at the same 144 spm,
so every "copy the video" tweak forced 45% longer strides (overreach / stair-climb / bent landings); (2) Unity crossfaded WalkMove->RunMove
unsynced (0.2 s, run always restarted at frame 0) -> leg scramble on every sprint start/stop.
Video facts: 12 frames/step @28.75 fps (144 spm); contact ~10-11 of 12 frames (almost no flight); head bob ~7 cm, lowest ~5 frames after heel strike
(mid-stance), highest at contact/push-off; swing shin horizontal at knee height with thigh ~vertical; landing leg ~20 deg ahead, straight.
New system (khalid_retarget.py, KEYED_LEGS=1): stance foot heel->flat->ball pivots locked to the ground (slide < 1 cm), hip = one smooth wave per step
(K_A, lowest at K_PLOW of stance), knee by IK from that hip, swing leg from thigh/knee angle key curves (K_SWING) matched in position+speed to the
stance at toe-off and heel strike; knee clamped at straight.
Khalid_Run_Keyed (3.1 m/s, wired): V=3.1/1.1 KEYS=25 K_D=.32 K_A=.02 K_PLOW=.5 K_ALPHA=20 K_PT=35 K_SWING=[(.22,-6,62),(.45,8,82),(.68,28,72),(.86,30,25)] + Forward2 torso/arms
Khalid_Run_Jog (2.2 m/s, matches the video, needs SprintSpeed 2.2): V=2.2/1.1 K_D=.45 K_SWING=[(.25,-4,62),(.5,10,82),(.75,30,65),(.9,28,22)]
Unity: WalkMove state now plays "Locomotion" (1D on Velocity: walk tree @2.0, run tree @3.0); Walk_SH cycleOffset .745 so its left heel strike lines up
with the run's (walk .975, run .23). RunMove state + old transitions left unused. Controller backup: AnimationTools/MainCharacterAnim.controller.backup_2026-10-09
ChaseNatural2_Wide: ChaseNatural2 settings (ChaseForward2 legs + LEAN_SCALE .6, ARM_SWING .58, FWD_KEEP 0, ARM_BIAS 16, ELBOW_FLAT .75, ELBOW_TARGET 95) + ARM_ABD=18 (arms opened 18 deg sideways: hands 23-38 cm from midline, was 15-28). Wired into the run tree (Locomotion) 2026-10-09.
ChaseNatural2_Chest: ChaseNatural2_Wide + HAND_CROSS=50 HAND_LIFT=25 SH_PROTRACT=14 SH_ELEV=6 YAW_SCALE=1.5 (forward hand comes in to the upper chest ~5-7 cm from midline; shoulders roll forward/back with the arm; chest twist up). Wired 2026-10-09. NOTE: fixed a bug where HAND_IN was only applied when ARM_ABD != 0 (introduced with ARM_ABD; Wide unaffected).
ChaseNatural2_HeadUp: ChaseNatural2_Chest + HEAD_OFF=19 (head tilt mean +4 -> -2 deg, i.e. level / chin slightly up). Wired 2026-10-09.
ChaseNatural2_Extend: ChaseNatural2_HeadUp + STANCE_STRAIGHT=.8 HZ_SMOOTH=20 DUTY_ADD=.06 SWING_DELAY=.3 LIFT_UNDELAYED=.5 (user: extend the legs + straight knee like the IRL frame). Stance leg ratio mean .984 / min .957 (was .946/.887, knee bent ~.89 through mid-stance); at each heel strike the back thigh is 30-34 deg behind (was ~14) with the back leg .92-.96 straight and foot ~.27 up (was folded, foot .4); front leg 19-20 deg ahead, straight. Hip height smooth sine (2nd diff .014 vs .037), 6 cm range. Heel strikes frames 5 and 18 -> Footstep events at 4/30 and 17/30 s. Wired 2026-10-09.
ChaseNatural2_Waist: ChaseNatural2_Extend + PELVIS_SYNTH=1 PELVIS_YAW_DEG=10 PELVIS_ROLL_DEG=4.5 SWAY_BU=.022 BACK_BEND=3 (user: back/waist should move with the run). The reference pelvis twist was ~a quarter cycle off Khalid's edited legs, so pelvis twist/drop/sway are now sine waves timed to his own feet: twist +-10 deg peaking at each touchdown (hip of the forward leg forward; chest still counter-rotates, so the lower back winds up between them), drop +-4.5 deg at mid-stance (swing side lower), sway +-2.2 cm over the stance foot, lower back side-bend 3 deg. Roll 6 deg overstretched the push-off leg (IK ratio 1.0), 4.5 keeps it <= .9975. Legs otherwise as Extend. Wired 2026-10-09.

=== 2026-10-10 Khalid_Run_IRL: rebuilt from the IRL Run.mov key poses (IRL_UPPER=1 + KEYED_LEGS=1) ===
Video analysis (frames extracted with ffmpeg, joints read by eye on 10 px grids, tools + comparison sheets in AnimationTools/IRL_analysis/):
 24 video frames per cycle @28.75 fps (0.835 s, 144 spm), runner ~2.2-2.3 m/s, contact ~11-12 of 24 frames per foot (almost no flight).
 landing F10/F22: leg straight 13-18 deg ahead, foot ~flat (toes up 5-10); stance knee 5-15 deg; heel rise from ~58% of stance; toe-off F20-21 leg straight ~40 deg behind, foot pitch ~-50.
 swing (video frame after the other foot lands: thigh deg, knee bend deg): t13 -38/12, t14 -28/35, t15 -23/54, t16 -16/67 (shin horizontal at knee height), t17 0/88, t18 21/102, t19 33/103, t20 40/92, t21 41/72, t22 33/40, t23 24/14.
 arms (left arm, + forward, t0 = left heel strike): back peak ~-83 (elbow at shoulder height) just before own-side landing, forward peak ~+32 three frames before the opposite landing; elbow 95-120; forearms angled in across the body.
 torso lean ~5-7 deg, neck forward, chin level/up; head lowest at mid-stance, highest at toe-off (~8 cm).
 Not measurable from a side view: pelvis/chest twist, hip drop, sideways sway -> tied to the measured legs/arms (twist from the actual foot positions, chest twist from the actual arm swing, drop/sway from foot loading).
Khalid_Run_IRL (game speed 3.1 m/s, WIRED): params in Blender ns['IRL_C4']: V=3.1/1.1 KEYS=25 KEYED_LEGS=1 K_D=.325 K_ALPHA=16 K_FLEX_C=2 K_FLEX_M=8 K_FLEX_T=4 K_MID=.4 K_P0=8 K_PT=62 K_HS=.12 K_HO=.55 K_A=.012 K_PLOW=.45 K_TAN_MAX=250
  K_SWING = [(.08,-28,6),(.16,-33,6),(.26,-36,8),(.32,-35,14)] + video swing t14..t23 mapped to s=.32+(t-13)/11*.68 (the extra flight at 3.1 m/s is spent with the trailing leg still extended, like the video at the other foot's landing)
  IRL_UPPER=1 IRL_ARM_SCALE=.92 IRL_ARM_ROT=22 IRL_ARM_ABD=16 IRL_LEAN=table+4 (Khalid's rest posture leans back ~4) IRL_NECK=6 IRL_HEAD=4 IRL_CHEST_YAW=8 IRL_SH=10 PELVIS_YAW_DEG=6 PELVIS_ROLL_DEG=3.5 SWAY_BU=.02 BACK_BEND=2 YAW_SCALE=1
  Intensity reduced vs the video: hip bob 3 cm (video ~8), arm swing x.92, stance knee <= ~15 deg. Stance leg ratio .93-.98, toe-off not overstretched (.976).
Khalid_Run_IRL_Jog (video speed 2.3 m/s, NOT wired; needs SprintSpeed ~2.3): same but V=2.3/1.1 K_D=.44 and the video swing mapped from s=.074 (Blender ns['IRL_JOG_0.44']).
Bugs fixed while building: arm angles must ADD the trunk lean (a leaning chest carries a hanging arm back); swing-curve slope at toe-off is capped (K_TAN_MAX) because a fully stretched leg has an infinite IK slope.
