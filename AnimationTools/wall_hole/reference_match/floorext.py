# continue the extracted floor splatter beyond the strip the reference shows (rows > ~70 = more than 28 cm from the wall)
import numpy as np
from PIL import Image
a=np.asarray(Image.open('WallHoleBloodFloor_q.png')).astype(np.float32)/255
H,W=a.shape[:2]
q=np.where(a[...,3:4]>0.01,(a[...,:3]-0.5*(1-a[...,3:4]))/np.maximum(a[...,3:4],1e-3)*2,1.0)   # back to tint values
m=a[...,3].copy()
rng=np.random.default_rng(21)
yy,xx=np.mgrid[0:H,0:W].astype(np.float32)
def vn(h,w,c):
    g=rng.random((int(h/c)+3,int(w/c)+3)); ys=np.linspace(0,h/c,h); xs=np.linspace(0,w/c,w)
    y0=ys.astype(int); x0=xs.astype(int); fy=(ys-y0)[:,None]; fx=(xs-x0)[None,:]
    return (g[y0][:,x0]*(1-fx)+g[y0][:,x0+1]*fx)*(1-fy)+(g[y0+1][:,x0]*(1-fx)+g[y0+1][:,x0+1]*fx)*fy
new=np.zeros((H,W),np.float32)
brk=vn(H,W,5)*0.6+vn(H,W,2)*0.4
def stamp(cx,cy,r,v=1.0):
    x1,x2=int(max(cx-r-1,0)),int(min(cx+r+2,W)); y1,y2=int(max(cy-r-1,0)),int(min(cy+r+2,H))
    if x2<=x1 or y2<=y1: return
    d=np.hypot(xx[y1:y2,x1:x2]-cx,yy[y1:y2,x1:x2]-cy)
    new[y1:y2,x1:x2]=np.maximum(new[y1:y2,x1:x2],v*np.clip(1.25-d/r,0,1))
# ragged flicks thrown diagonally away from the wall towards +X (= decreasing column) like in the reference
for k in range(11):
    x0=rng.uniform(220,400); y0=rng.uniform(18,40)
    ang=np.deg2rad(rng.uniform(100,150))
    L=rng.uniform(45,150); wid=rng.uniform(3,8)
    ph=rng.uniform(0,6)
    for s_ in np.linspace(0,1,int(L*1.5)):
        cx=x0+np.cos(ang)*s_*L+np.sin(s_*7+ph)*2.0; cy=y0+np.sin(ang)*s_*L
        r=wid*(1-0.75*s_)*(0.7+0.6*abs(np.sin(s_*9+ph)))+0.5
        stamp(cx,cy,r,1.0 if rng.random()>0.12 else 0.0)
    for _ in range(int(rng.integers(2,6))):        # satellite drops along the flick
        s_=rng.uniform(0.3,1.1); stamp(x0+np.cos(ang)*s_*L+rng.normal(0,6),y0+np.sin(ang)*s_*L+rng.normal(0,6),rng.uniform(1,3))
# irregular blotches near the base
for _ in range(14):
    cx=rng.normal(300,60); cy=rng.uniform(35,80); 
    for _ in range(5): stamp(cx+rng.normal(0,6),cy+rng.normal(0,4),rng.uniform(3,7))
for _ in range(70):
    stamp(rng.normal(280,80),abs(rng.normal(0,60))+55,rng.uniform(0.8,2.5))
new*=np.clip(0.55+0.8*(brk-0.3),0,1.2)
n=vn(H,W,4)
new=np.clip(new*(0.8+0.4*n),0,1)
new=np.where(new>0.35,np.clip((new-0.35)/0.3,0,1),0)
# keep the extracted reference strip as is; the synthetic part only adds outside it and blends in near it
keep=np.clip((yy-28)/20,0,1)
add=new*keep
qn=np.stack([0.83-0.05*n,0.52-0.05*n,0.42-0.04*n],-1)
take=add>m
q=np.where(take[...,None],qn,q); m=np.maximum(m,add)
t=np.zeros((H,W,4),np.float32)
t[...,:3]=np.clip(q*0.5,0,1)*m[...,None]+0.5*(1-m[...,None]); t[...,3]=m
Image.fromarray((np.clip(t,0,1)*255+0.5).astype(np.uint8),'RGBA').save('D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/Assets/Textures/WallHoleBloodFloor.png')
out=np.full((H,W,3),0.75)*np.where(m[...,None]>0,q,1)*m[...,None]+0.75*(1-m[...,None])
Image.fromarray((np.clip(out,0,1)*255).astype(np.uint8)).crop((0,0,512,260)).resize((1024,520)).save('floor_vis2.png')
print('ok')
