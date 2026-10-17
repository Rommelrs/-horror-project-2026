import numpy as np
from PIL import Image, ImageFilter
ref = np.asarray(Image.open('../images/121.webp').convert('RGB')).astype(np.float32)/255
cln = np.asarray(Image.open('clean_fog.png').convert('RGB')).astype(np.float32)/255
print(ref.shape, cln.shape)
def gray(a): return a@np.array([0.299,0.587,0.114],np.float32)
def grad(g):
    gx=np.zeros_like(g); gy=np.zeros_like(g)
    gx[:,1:-1]=g[:,2:]-g[:,:-2]; gy[1:-1]=g[2:]-g[:-2]
    return np.hypot(gx,gy)
gr=grad(gray(ref)); gc=grad(gray(cln))
H,W=gr.shape
# evaluation mask: exclude the blood area (x 780..1100) and the UI overlays
mask=np.ones((H,W),bool); mask[:, 780:1110]=False; mask[:,:60]=False; mask[640:, 1680:]=False; mask[:140,1780:]=False; mask[790:, :420]=False
ys,xs=np.nonzero(mask)
sel=np.random.default_rng(0).choice(len(ys),60000,replace=False); ys=ys[sel]; xs=xs[sel]
cx,cy=W/2,H/2
def score(s,dx,dy):
    # sample ref at transformed coords: ref(s*(x-cx)+cx+dx)
    xr=np.round(s*(xs-cx)+cx+dx).astype(int); yr=np.round(s*(ys-cy)+cy+dy).astype(int)
    ok=(xr>=0)&(xr<W)&(yr>=0)&(yr<H)
    a=gc[ys[ok],xs[ok]]; b=gr[yr[ok],xr[ok]]
    a=a-a.mean(); b=b-b.mean()
    return (a*b).sum()/np.sqrt((a*a).sum()*(b*b).sum())
best=None
for s in np.arange(0.96,1.045,0.01):
    for dx in range(-24,25,2):
        for dy in range(-24,25,2):
            v=score(s,dx,dy)
            if best is None or v>best[0]: best=(v,s,dx,dy)
print('coarse',best)
v,s0,dx0,dy0=best
for s in np.arange(s0-0.01,s0+0.0101,0.0025):
    for dx in np.arange(dx0-2,dx0+2.01,0.5):
        for dy in np.arange(dy0-2,dy0+2.01,0.5):
            v=score(s,dx,dy)
            if v>best[0]: best=(v,s,dx,dy)
print('fine',best)
np.save('reg.npy',np.array(best[1:]))
