# inner faces of the hole. 6 columns of 64px = faces in HOLE order: 0 top, 1 +X side (seen from the alley), 2 lower-right, 3 lower-left, 4 -X side, 5 upper-left
# v = 1 at the front edge. tint values measured from the reference (fogged-space multipliers)
import numpy as np
from PIL import Image
rng=np.random.default_rng(11)
SEG,H=64,64
def vnoise(h,w,c):
    g=rng.random((int(h/c)+3,int(w/c)+3)); ys=np.linspace(0,h/c,h); xs=np.linspace(0,w/c,w)
    y0=ys.astype(int); x0=xs.astype(int); fy=(ys-y0)[:,None]; fx=(xs-x0)[None,:]
    return (g[y0][:,x0]*(1-fx)+g[y0][:,x0+1]*fx)*(1-fy)+(g[y0+1][:,x0]*(1-fx)+g[y0+1][:,x0+1]*fx)*fy
faces=[]
v=(1-np.arange(H)/(H-1))[:,None]*np.ones((1,SEG))
for k in range(6):
    n=vnoise(H,SEG,6)*0.6+vnoise(H,SEG,2)*0.4
    if k in (2,3):     # bottom faces: soaked lip along the front edge, irregular inner border
        reach=(0.18+0.3*vnoise(1,SEG,8)[0])[None,:]
        m=np.clip((v-(1-reach))/0.12,0,1)*(0.8+0.2*n)
        q=np.stack([0.74-0.05*n,0.47-0.05*n,0.41-0.04*n],-1)
    elif k==0:         # top face: lighter brown-red
        m=np.clip(0.85*(0.55+0.45*v)*(0.75+0.5*n),0,1)
        q=np.stack([np.full_like(m,0.72),np.full_like(m,0.60),np.full_like(m,0.53)],-1)
    else:              # side faces: soaked dark brown, a little redder at the front
        m=np.clip(0.95*(0.8+0.2*v)*(0.85+0.3*n),0,1)
        red=np.clip((v-0.5)/0.5,0,1)
        q=np.stack([0.60+0.08*red,0.49-0.04*red,0.42-0.04*red],-1)*(0.95+0.1*n[...,None])
    t=np.zeros((H,SEG,4),np.float32)
    t[...,:3]=np.clip(q*0.5,0,1)*m[...,None]+0.5*(1-m[...,None]); t[...,3]=m
    faces.append(t)
img=np.concatenate(faces,1)
Image.fromarray((np.clip(img,0,1)*255+0.5).astype(np.uint8),'RGBA').save('D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/Assets/Textures/WallHoleBloodRim.png')
print('ok',img.shape)
