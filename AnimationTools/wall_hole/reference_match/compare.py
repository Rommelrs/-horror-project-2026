import numpy as np, sys
from PIL import Image
def load(p): return np.asarray(Image.open(p).convert('RGB')).astype(np.float32)/255
cur=load('cur.png'); cf=load('clean_fog.png'); ra=load('ref_aligned.png')
q=np.load('q.npy'); m=np.load('m2.npy')
def blur(a,r=1):
    out=a.copy()
    for ax in (0,1):
        acc=np.zeros_like(out)
        for k in range(-r,r+1): acc+=np.roll(out,k,axis=ax)
        out=acc/(2*r+1)
    return out
qc=blur(cur,1)/np.maximum(blur(cf,1),0.02)
H,W,_=cf.shape
wall=np.zeros((H,W),bool); wall[250:735,820:1030]=True
floor=np.zeros((H,W),bool); floor[760:833,760:1110]=True
for name,z in (('wall',wall),('floor',floor)):
    sel=z&(m>0.6)
    print(name,'target q',np.median(q[sel],0).round(3),'current q',np.median(qc[sel],0).round(3),'n',sel.sum())
crop=(slice(200,833),slice(760,1130))
# reference in the current render's tone: clean_fog * q
tgt=cf*np.where(m[...,None]>0,q,1)
vis=np.concatenate([ra[crop],np.clip(tgt[crop],0,1),cur[crop]],1)
Image.fromarray((np.clip(vis,0,1)*255).astype(np.uint8)).save('cmp_side.png')
