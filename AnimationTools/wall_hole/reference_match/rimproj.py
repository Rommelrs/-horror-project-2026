# project the reference onto the 6 inner faces of the hole (rim mesh UVs: face k -> u in [k/6,(k+1)/6], v=1 front, v=0 back)
import numpy as np, json
from PIL import Image
cam=json.load(open('cam.json')); V=np.array(cam['V']).reshape(4,4); P=np.array(cam['P']).reshape(4,4); W=cam['w']; H=cam['h']
q=np.load('q.npy')
camW=np.linalg.inv(V)@np.array([0,0,0,1.0]); C=camW[:3]
HOLE=np.array([(-18.736,3.473),(-18.267,3.322),(-18.355,2.977),(-18.686,2.785),(-18.959,2.926),(-18.904,3.213)])
cen=HOLE.mean(0)
ZF,ZB=-76.455,-76.935
def to_screen(p):
    h=np.concatenate([p,np.ones(p.shape[:-1]+(1,))],-1)@(P@V).T
    return (h[...,0]/h[...,3]*0.5+0.5)*W-0.5,(1-(h[...,1]/h[...,3]*0.5+0.5))*H-0.5
def samp(img,x,y):
    x=np.clip(x,0,W-1.001); y=np.clip(y,0,H-1.001)
    x0=np.floor(x).astype(int); y0=np.floor(y).astype(int); fx=(x-x0)[...,None]; fy=(y-y0)[...,None]
    return (img[y0,x0]*(1-fx)+img[y0,x0+1]*fx)*(1-fy)+(img[y0+1,x0]*(1-fx)+img[y0+1,x0+1]*fx)*fy
def inside(px,py):
    ins=np.zeros(px.shape,bool)
    for i in range(6):
        (x1,y1),(x2,y2)=HOLE[i],HOLE[(i+1)%6]
        ins^=((y1>py)!=(y2>py))&(px<(x2-x1)*(py-y1)/(y2-y1+1e-9)+x1)
    return ins
SEG=64
faces=[];vis_frac=[]
for k in range(6):
    a=HOLE[k]; b=HOLE[(k+1)%6]
    a=a+(cen-a)/np.linalg.norm(cen-a)*0.002; b=b+(cen-b)/np.linalg.norm(cen-b)*0.002
    u=(np.arange(SEG)+0.5)/SEG; v=1-(np.arange(SEG)+0.5)/SEG
    U,Vv=np.meshgrid(u,v)
    xy=a[None,None,:]*(1-U[...,None])+b[None,None,:]*U[...,None]
    z=ZF+(1-Vv)*(ZB-ZF)
    pts=np.concatenate([xy,z[...,None]],-1)
    # visible = ray from camera to the point passes through the opening on the front plane
    d=pts-C; t=(-76.46-C[2])/d[...,2]; hit=C+d*t[...,None]
    vis=inside(hit[...,0],hit[...,1])
    # and the face looks towards the camera
    edge=b-a; nrm=np.array([-edge[1],edge[0]]); 
    if np.dot(nrm,cen-(a+b)/2)<0: nrm=-nrm
    facing=(nrm[None,None,0]*(C[0]-xy[...,0])+nrm[None,None,1]*(C[1]-xy[...,1]))>0
    vis&=facing
    sx,sy=to_screen(pts)
    qq=np.clip(samp(q,sx,sy),0.05,1.0)
    faces.append((qq,vis)); vis_frac.append(vis.mean())
print('visible fraction per face',np.round(vis_frac,2))
# fill hidden texels: per face, use the visible part of the same face (row-wise mean) or the closest similar face
out=[]
for k,(qq,vis) in enumerate(faces):
    if vis.mean()>0.6:
        f=qq.copy()
        rowmean=np.array([qq[r][vis[r]].mean(0) if vis[r].any() else qq[r].mean(0) for r in range(SEG)])
        f[~vis]=rowmean[np.nonzero(~vis)[0]]
    else:
        # hidden face: borrow from the most similar visible face (sides from side 1, bottoms from bottom 2, top from top 0)
        src={0:0,1:1,2:2,3:2,4:1,5:1}[k]
        sq,sv=faces[src]
        f=sq[:, ::-1].copy() if sv.mean()>0.6 else np.full_like(sq,0.7)
    out.append(f)
img=np.concatenate(out,1)
t=np.zeros(img.shape[:2]+(4,),np.float32)
t[...,:3]=np.clip(img*0.5,0,1); t[...,3]=1.0
Image.fromarray((t*255+0.5).astype(np.uint8),'RGBA').save('D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/Assets/Textures/WallHoleBloodRim.png')
vis_img=np.clip(img*0.75,0,1)
Image.fromarray((vis_img*255).astype(np.uint8)).resize((768,128)).save('rim_vis.png')
