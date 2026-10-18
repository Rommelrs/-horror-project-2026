#!/bin/sh
# usage: sh shots.sh TAG -> shots/c_TAG.png (concept front | front | concept side | side) and shots/c_TAGb.png (straight | q34 | profile | game | gameside)
cd "$(dirname "$0")"
python cmp.py shots/c_$1.png ref/concept_front.png shots/$1_front.png ref/concept_side.png shots/$1_side.png
python cmp.py shots/c_$1b.png shots/$1_straight.png shots/$1_q34.png shots/$1_profile.png shots/$1_game.png shots/$1_gameside.png
python cmp.py shots/c_$1c.png shots/$1_ufront.png shots/$1_uside.png ref/concept_front_x3.png
