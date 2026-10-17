import numpy as np, json
from PIL import Image
cam=json.load(open('cam.json')); V=np.array(cam['V']).reshape(4,4); P=np.array(cam['P']).reshape(4,4); W=cam['w']; H=cam['h']
q=np.load('q.npy'); mask=np.load('m2.npy')
def to_screen(X,Y,Z):
    pts=np.stack([X,Y,Z,np.ones_like(X)],-1)@(P@V).T
    return (pts[...,0]/pts[...,3]*0.5+0.5)*W-0.5, (1-(pts[...,1]/pts[...,3]*0.5+0.5))*H-0.5
def samp(img,x,y):
    x=np.clip(x,0,W-1.001); y=np.clip(y,0,H-1.001)
    x0=np.floor(x).astype(int); y0=np.floor(y).astype(int); fx=x-x0; fy=y-y0
    if img.ndim==3: fx=fx[...,None]; fy=fy[...,None]
    return (img[y0,x0]*(1-fx)+img[y0,x0+1]*fx)*(1-fy)+(img[y0+1,x0]*(1-fx)+img[y0+1,x0+1]*fx)*fy
def smooth(e0,e1,x):
    t=np.clip((x-e0)/(e1-e0),0,1); return t*t*(3-2*t)
HOLE=np.array([(-18.736,3.473),(-18.267,3.322),(-18.355,2.977),(-18.686,2.785),(-18.959,2.926),(-18.904,3.213)])
def inside_poly(X,Y):
    ins=np.zeros(X.shape,bool)
    for i in range(6):
        (x1,y1),(x2,y2)=HOLE[i],HOLE[(i+1)%6]
        ins^=((y1>Y)!=(y2>Y))&(X<(x2-x1)*(Y-y1)/(y2-y1+1e-9)+x1)
    return ins
def save(name,qv,m):
    t=np.zeros(qv.shape[:2]+(4,),np.float32)
    t[...,:3]=np.clip(qv*0.5,0,1)*m[...,None]+0.5*(1-m[...,None]); t[...,3]=np.clip(m,0,1)
    Image.fromarray((t*255+0.5).astype(np.uint8),'RGBA').save(name)

# wall: x -19.6..-17.7, y 0.85..4.25, z=-76.46 ; column 0 = maxX
WX0,WX1,WY0,WY1=-19.6,-17.7,0.85,4.25; PPM=320
w=int(round((WX1-WX0)*PPM)); h=int(round((WY1-WY0)*PPM))
cols,rows=np.meshgrid(np.arange(w),np.arange(h))
X=WX1-(cols+0.5)/PPM; Y=WY1-(rows+0.5)/PPM
sx,sy=to_screen(X,Y,np.full(X.shape,-76.46))
qv=samp(q,sx,sy); m=samp(mask,sx,sy)
m*=smooth(-19.3,-19.12,X)*(1-smooth(-18.1,-17.95,X))*(1-smooth(3.62,3.72,Y))
ins=inside_poly(X,Y)
# dense band hugging the cut edge (the extraction can't separate it from the hole outline): heavier on the +X (screen-left) side and below
rng=np.random.default_rng(3)
def vn(h,w,c):
    g=rng.random((int(h/c)+3,int(w/c)+3)); ys=np.linspace(0,h/c,h); xs=np.linspace(0,w/c,w)
    y0=ys.astype(int); x0=xs.astype(int); fy=(ys-y0)[:,None]; fx=(xs-x0)[None,:]
    return (g[y0][:,x0]*(1-fx)+g[y0][:,x0+1]*fx)*(1-fy)+(g[y0+1][:,x0]*(1-fx)+g[y0+1][:,x0+1]*fx)*fy
dist=np.full(X.shape,1e9)
for i in range(6):
    (x1,y1),(x2,y2)=HOLE[i],HOLE[(i+1)%6]
    ex,ey=x2-x1,y2-y1; t=np.clip(((X-x1)*ex+(Y-y1)*ey)/(ex*ex+ey*ey),0,1)
    dist=np.minimum(dist,np.hypot(X-(x1+t*ex),Y-(y1+t*ey)))
cxh,cyh=HOLE[:,0].mean(),HOLE[:,1].mean()
ang=np.arctan2(Y-cyh,X-cxh)
side=np.clip(np.cos(ang),0,1)            # +X side
low=np.clip(-np.sin(ang),0,1)
bw=0.025+0.095*side**1.1+0.05*low*(0.4+side)+0.015*vn(h,w,10)
n1=vn(h,w,3)*0.6+vn(h,w,8)*0.4
edge=np.clip((1-dist/bw)*1.25+(n1-0.5)*0.9,0,1)*(~ins)
qe=np.stack([0.80-0.06*n1,0.52-0.06*n1,0.45-0.05*n1],-1)
take=edge>m
qv=np.where(take[...,None],qe*edge[...,None]+qv*(1-edge[...,None]),qv)
m=np.maximum(m,edge)
ring=(m>0.6)&~ins&(np.abs(X+18.62)<0.45)&(np.abs(Y-3.13)<0.45)
qin=np.median(qv[ring],0); print('rim q',qin)
qv[ins]=qin*0.85; m[ins]=1.0
# thicken the thin drips under the hole a touch (1-2 texels) so they survive sampling / alignment error
# join the broken pieces of each drip vertically (runs are continuous in the reference)
def vblur(a,r):
    acc=np.zeros_like(a)
    for k in range(-r,r+1): acc+=np.roll(a,k,axis=0)
    return acc/(2*r+1)
zone=(Y<2.80)
mv=vblur(m,5); qv_b=vblur(qv*m[...,None],5)/np.maximum(mv[...,None],1e-3)
joined=np.maximum(m,mv*1.15)
qv=np.where((zone&(mv>m))[...,None],qv_b,qv); m=np.where(zone,np.clip(joined,0,1),m)
below=(Y<2.60)[...,None]
qd=qv.copy(); md=m.copy()
for dx in (-2,-1,1,2):
    qs=np.roll(qv,dx,axis=1); ms=np.roll(m,dx,axis=1)
    better=(ms>md)
    md=np.where(better,ms*(0.92 if abs(dx)==1 else 0.8),md)
    qd=np.minimum(qd,np.where(ms[...,None]>0.3,qs,qd))
qv=np.where(below,qd,qv); m=np.where(below[...,0],np.maximum(m,md),m)
save('WallHoleBlood_q.png',qv,m)
# floor: x -19.6..-17.6, z -76.43..-74.43 on y=0.846 ; top row = wall side, column 0 = maxX
FX0,FX1,FZ0,FZ1=-19.6,-17.6,-76.43,-74.43; FPM=256
fw=int(round((FX1-FX0)*FPM)); fh=int(round((FZ1-FZ0)*FPM))
cols,rows=np.meshgrid(np.arange(fw),np.arange(fh))
X=FX1-(cols+0.5)/FPM; Z=FZ0+(rows+0.5)/FPM
sx,sy=to_screen(X,np.full(X.shape,0.846),Z)
inview=((sx>1)&(sx<W-2)&(sy>1)&(sy<H-2)).astype(np.float32)
qv=samp(q,sx,sy); m=samp(mask,sx,sy)*inview
m*=smooth(-19.45,-19.3,X)*(1-smooth(-17.95,-17.8,X))
save('WallHoleBloodFloor_q.png',qv,m)
print('wall',w,h,'floor',fw,fh,'floor cover',(m>0.5).mean())
