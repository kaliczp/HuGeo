/*
  TECA: TÉrbeli Coordináta Átszámító alkalmazás
  Brolly Gábor
  Koordináta transzformáció: WGS84 --> EOV országos paraméterkészlet
  Az alkalmazás a maradékokkal javítja az országos transzformációt
*/

#include <stdlib.h>
#include <stdio.h>
#include <math.h>
#include <string.h>

#define PI 3.1415926535897932384626433832795
#define FIK 0.82205007768932923073105835195814 //kezdõpont szélessége: 47-06-00
#define RG 6379743.001 //Gauss-gömb sugara
#define M0 0.99993 //EOV vetületi redukció

typedef struct { double la, fi, he, x, y, h; } COORD;

void transform(COORD *pt)
{ double Xwgs, Ywgs, Zwgs; //WGS84 koordináták
  double Xiugg, Yiugg, Ziugg; //IUGG67 koordináták
  double mr [3][3]; //forgatási mátrix elemei
  double dx, dy, dz; //eltolás
  double m; //méretarány

  double Fi, La, He; //földrajzi szélesség, hosszúság, [radiánban], ellipszoid feletti magasság
  double N;//harántgörbületi sugár
  double a, b, ea, eb; //ellipszoidi paraméterek
  double t, p;//segédparaméterek

  double la, fi; //koordináták a G-gömbön
  double n, k; //vetületi állandók
  double Lk; //EOV vetületi kezdõpont ellipszoidi koord

  double sla, sfi; //segédkoordináták
  double x, y; //EOV koordináták

  La=pt->la; Fi=pt->fi; He=pt->he;
  
  //-------------- Áttérés derékszögû koordinátákra

  Fi=PI*Fi/180;
  La=PI*La/180;
  
  a=6378137; //wgs84
  b=6356752.31425;//wgs84
  ea=sqrt((a*a-b*b)/(a*a));//1. num. excentricitás wgs84
  eb=sqrt((a*a-b*b)/(b*b));//2. num. excentricitás wgs84
  N=a*a/sqrt(a*a*cos(Fi)*cos(Fi)+b*b*sin(Fi)*sin(Fi)); //wgs84

  Xwgs=(N+He)*cos(Fi)*cos(La);
  Ywgs=(N+He)*cos(Fi)*sin(La);
  Zwgs=((N*b*b)/(a*a)+He)*sin(Fi);
  
  //--------------- 7 paraméteres transzformáció (országos paraméterkészlet)
  
  dx=-54.595;
  dy= 72.495;
  dz= 14.817;
  m=1 + 0.000001 * (-1.998606);
  
  mr[0][0]=	 0.99999999999868900000;
  mr[0][1]=	 0.00000141867263403686;
  mr[0][2]= -0.00000078073425578426;
  
  mr[1][0]=	-0.00000141867148993435;
  mr[1][1]=	 0.99999999999792000000;
  mr[1][2]=	 0.00000146541722506840;

  mr[2][0]=  0.00000078073633472995;
  mr[2][1]=	-0.00000146541611746105;
  mr[2][2]=	 0.99999999999862200000;

  Xiugg= dx + m*( mr[0][0]*Xwgs + mr[1][0]*Ywgs + mr[2][0]*Zwgs);
  Yiugg= dy + m*( mr[0][1]*Xwgs + mr[1][1]*Ywgs + mr[2][1]*Zwgs);
  Ziugg= dz + m*( mr[0][2]*Xwgs + mr[1][2]*Ywgs + mr[2][2]*Zwgs);

  //-------------- Áttérés földrajzi koordinátákra
  //A WGS84-nél használt ellipszoidi változók (a, b, ea, eb, N, La, Fi, He) új értéket kapnak

  a=6378160.0;//IUGG67 nagytengely
  b=6356774.516;//IUGG67 kistengely
  ea=0.0818205679407; //IUGG67, 1. num. excentricitás
  eb=0.0820958289928;//IUGG67, 2. num. excentricitás

  La=atan2(Yiugg,Xiugg);//IUGG67-re vonatkozik
  p=sqrt(Xiugg*Xiugg+Yiugg*Yiugg);
  t=atan2(Ziugg*a,p*b);
  Fi=atan2(Ziugg+eb*eb*b*sin(t)*sin(t)*sin(t),p-ea*ea*a*cos(t)*cos(t)*cos(t));//IUGG67-re vonatkozik
  N=a*a/sqrt(a*a*cos(Fi)*cos(Fi)+b*b*sin(Fi)*sin(Fi));//IUGG67-re vonatkozik
  He=p/cos(Fi)-N;//IUGG67-re vonatkozik

  //-------------- Áttérés a Gauss-gömbre
  
  n=1.000719704936;
  Lk=0.3324602953246920;
  k=1.0031100083;

  la=n*(La-Lk);
  fi=2*(atan(k*pow(tan(0.5*Fi+0.25*PI),n)*pow((1-ea*sin(Fi))/(1+ea*sin(Fi)),0.5*n*ea))-0.25*PI);


  //-------------- Vetületi egyenletek
  
  sfi=asin( sin(fi)*cos(FIK) - cos(fi)*sin(FIK)*cos(la) );
  sla=asin( (cos(fi)*sin(la))/cos(sfi) );

  y=M0*RG*sla + 650000; //EOVY
  x=M0*RG*log(tan(0.5*sfi + 0.25*PI)) + 200000;//EOVX

  pt->x=x; pt->y=y; pt->h=He;

}

//----------------------------------------------------------------

int residual(COORD *p, double *mdx, double *mdy, double *mdz)
{ /* 1, 2, 3, 4: matematikai K-rendszer síknegyedei
  */

  double x, y, x0, y0, u, v;//x, y: aktuális pont földrajzi koord, x0, y0: raszter bal felsõ sarok
  double x1, y1, x2, y2, x3, y3, x4, y4, t1, t2, t3, t4;//négy legközelebbi gridpont
  double d;//cella oldalhossz
  double *p1, *p2, *p3, *p4, *pm; //p: raszterra mutat
  int w, i1, i2, i3, i4, j1, j2, j3, j4; //raszterkoordináták (i: col, j: row), w: oszlopok száma
    
  d=0.06;
  w=116;
  x0=16.05; y0=48.65; //bal felsõ sarok
  x=p->la; y=p->fi; //EOV --> matematikai K rendszer
  u=(x-x0)/d; v=(y0-y)/d; //u, v paraméter a pont négy gridponttól mért távolságának leírásához
  
  //kikeressük a négy szomszédos gridpontot
  x1=x0+ceil(u)*d;  y1=y0-floor(v)*d;  i1=floor((x1-x0)/d);  j1=floor((y0-y1)/d);
  x2=x0+floor(u)*d; y2=y0-floor(v)*d;  i2=floor((x2-x0)/d);  j2=floor((y0-y2)/d);
  x3=x0+floor(u)*d; y3=y0-ceil(v)*d;   i3=floor((x3-x0)/d);  j3=floor((y0-y3)/d);
  x4=x0+ceil(u)*d;  y4=y0-ceil(v)*d;   i4=floor((x4-x0)/d);  j4=floor((y0-y4)/d);
 
  //súlyok kiszámítása
  t1=fabs((x-x1)*(y-y1));
  t2=fabs((x-x2)*(y-y2));
  t3=fabs((x-x3)*(y-y3));
  t4=fabs((x-x4)*(y-y4));
  
  //rámutatunk a négy elem dx értékére
  pm=mdx;
  
  p1=pm+w*j1+i1;
  p2=pm+w*j2+i2;
  p3=pm+w*j3+i3;
  p4=pm+w*j4+i4;

  if(*p1==-256 || *p2==-256 || *p3==-256 || *p4==-256) { puts("Mo-on kivuli pont\n"); return 1; }
  p->x+=(t3*(*p1)+t4*(*p2)+t1*(*p3)+t2*(*p4))/(d*d);

  //rámutatunk a négy elem dy értékére
  pm=mdy;
  
  p1=pm+w*j1+i1;
  p2=pm+w*j2+i2;
  p3=pm+w*j3+i3;
  p4=pm+w*j4+i4;

  p->y+=(t3*(*p1)+t4*(*p2)+t1*(*p3)+t2*(*p4))/(d*d);
  
  //rámutatunk a négy elem dh értékére
  pm=mdz;

  p1=pm+w*j1+i1;
  p2=pm+w*j2+i2;
  p3=pm+w*j3+i3;
  p4=pm+w*j4+i4;

  p->h+=(t3*(*p1)+t4*(*p2)+t1*(*p3)+t2*(*p4))/(d*d);
  
  return 0;
}


//----------------------------------------------------------------


void main (void)
{ FILE *FilIn, *FilOut;
  char FilNamIn[256], FilNamOut[256], buf[256];
  COORD *p;
  int j, i, n, h, w;
  double *mdx, *mdy, *mdz, *pdx, *pdy, *pdz;
  float *md, *pmd;
  
  p=(COORD*) malloc(sizeof(COORD));
  
  FilIn=fopen("grid_delta.dat","rb");//javítási tömb bil formátum
  if(FilIn==NULL) { puts("Nem talalhato: grid_delta.dat\n"); return; }
  h=50; w=116; //grid_delta raszter magasság, szélesség
  md=(float*)calloc(3*h*w,sizeof(float));
  fread(md,sizeof(float),3*h*w,FilIn);//beolvassuk az egész javítási tömböt
  fclose(FilIn);

  //ez a három tömb tartalmazza a javításokat szétbontva x, y, z irányba
  mdx=(double*)calloc(h*w,sizeof(double));
  mdy=(double*)calloc(h*w,sizeof(double));
  mdz=(double*)calloc(h*w,sizeof(double));
  
  //szétbontás x, y, z irányra
  for(j=0;j<h;j++)
  { for(i=0;i<w;i++) { pmd=md+3*j*w+i;     pdx=mdx+j*w+i; *pdx=*pmd; }
    for(i=0;i<w;i++) { pmd=md+(3*j+1)*w+i; pdy=mdy+j*w+i; *pdy=*pmd; }
	for(i=0;i<w;i++) { pmd=md+(3*j+2)*w+i; pdz=mdz+j*w+i; *pdz=*pmd; }
  }
  free(md);
  
  puts("Adja meg az input fajlnevet [kiterjesztessel]:\n");
  gets(FilNamIn); 

  FilIn=fopen(FilNamIn, "rt");
  if(FilIn==NULL) { printf("Hibas fajlnev: %s\n",FilNamIn); return; }
  
  puts("Adja meg az output fajlnevet [kiterjesztessel]:\n");
  gets(FilNamOut);
  FilOut=fopen(FilNamOut, "wt");

  n=0;
  while(fgets(buf, 256, FilIn))
  { if(sscanf(buf,"%lf%lf%lf",&p->la, &p->fi, &p->he)<3) { printf("Hibas sor: %s\n",buf); continue;} 
    transform(p);
	residual(p,mdx,mdy,mdz);
	fprintf(FilOut,"%.3lf\t%.3lf\t%.3lf\n",p->x,p->y,p->h);
	n++;
  }
  printf("\rTranszformalt pontok szama: %d\n",n);
  fclose(FilIn);
  fclose(FilOut);
}