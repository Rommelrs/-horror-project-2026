import numpy as np
from PIL import Image
def load(p): return np.asarray(Image.open(p).convert('RGB')).astype(np.float32)/255
ra=load('ref_aligned.png'); cf=load('clean_fog.png')
H,W,_=cf.shape
def blur(a,r=1):
    out=a.copy()
    for ax in (0,1):
        acc=np.zeros_like(out)
        for k in range(-r,r+1): acc+=np.roll(out,k,axis=ax)
        out=acc/(2*r+1)
    return out
def boxblur(a,r):
    # fast box blur via cumulative sums, edge-padded
    for ax in (0,1):
        pad=[(r+1,r) if i==ax else (0,0) for i in range(a.ndim)]
        c=np.cumsum(np.pad(a,pad,mode='edge'),axis=ax)
        n=2*r+1
        a=(np.take(c,np.arange(n,c.shape[ax]),axis=ax)-np.take(c,np.arange(0,c.shape[ax]-n),axis=ax))/n
    return a
def smooth(e0,e1,x):
    t=np.clip((x-e0)/(e1-e0),0,1); return t*t*(3-2*t)
rab=blur(ra,1); cfb=blur(cf,1)
red=cfb[...,0]-0.5*(cfb[...,1]+cfb[...,2]); sym=np.clip((red-boxblur(red,25)-0.012)/0.02,0,1)
# weight: exclude the blood area (rough screen polygon) and the symbols
wgt=np.ones((H,W),np.float32)
wgt[200:833,830:1020]=0
wgt[735:833,740:1110]=0
wgt*=(1-sym)
ratio=rab/np.maximum(cfb,0.02)
R=60
num=boxblur(ratio*wgt[...,None],R); den=boxblur(wgt,R)[...,None]
gain=num/np.maximum(den,1e-3)
# second, wider pass where the first had too little support
num2=boxblur(ratio*wgt[...,None],150); den2=boxblur(wgt,150)[...,None]
gain=np.where(den>0.15,gain,num2/np.maximum(den2,1e-3))
q=rab/np.maximum(cfb*gain,0.02)
q_sharp=ra/np.maximum(cf*gain,0.02)
d=1-q[...,1]
m=smooth(0.08,0.30,d)*smooth(0.0,0.06,q[...,0]-q[...,1])
m*=(1-sym*0.85)
m*=smooth(0.05,0.13,q[...,0]-q[...,1])        # must shift towards red (kills brick-edge misregistration dashes)
m*=np.maximum(smooth(0.99,0.93,q[...,0]),smooth(0.45,0.6,d))      # blood darkens red too; the (recoloured) symbols do not
zone=np.zeros((H,W),bool); zone[200:833,760:1130]=True
m*=zone
m=blur(m.astype(np.float32),1)
m=smooth(0.12,0.45,m)                               # firm mask: thin streaks keep their full strength
q=np.where(m[...,None]>0.05,np.minimum(q,q_sharp*0.5+q*0.5),q)   # keep thin-streak contrast
np.save('q.npy',q.astype(np.float32)); np.save('m2.npy',m.astype(np.float32))
for name,(y0,y1,x0,x1) in {'drips':(450,650,900,960),'halo_left':(300,360,840,870),'floor':(770,830,820,980),'plainwall':(450,650,1150,1250),'wall_left':(450,650,780,830)}.items():
    sl=(slice(y0,y1),slice(x0,x1)); sel=m[sl]>0.5
    print(name,'cover %.2f'%sel.mean(),'q(blood)',np.median(q[sl][sel],0).round(2) if sel.any() else '-','q all',np.median(q[sl].reshape(-1,3),0).round(2))
vis=np.concatenate([ra[200:833,760:1130],np.clip(q[200:833,760:1130]*0.6,0,1),np.repeat(m[200:833,760:1130,None],3,2)],1)
Image.fromarray((np.clip(vis,0,1)*255).astype(np.uint8)).save('extract_vis2.png')
