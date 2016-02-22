
#define NUM1 8
#define NUM2 9
#define NUM3 14
#define NUM4 15
byte SPI_DATA[2] = {
  0, 0};
int SPI_DATA_POS = 0;
int Mode = 0;//0:none 3:barBeat
int Bar = 0;
int Beat = 0;
int Tempo = 0;
int LightMax = 0;
byte Light;
void setup(){
//Serial.begin(9600);
  for(int i =0 ; i<= 9; i++){
    pinMode(i, OUTPUT);
    digitalWrite(i, LOW); 
  }
  pinMode(14, OUTPUT);
  digitalWrite(14, LOW);
  pinMode(15, OUTPUT);
  digitalWrite(15, LOW); 

  //SPI
  pinMode(SS, INPUT);
  pinMode(13, INPUT);
  pinMode(MOSI, INPUT);
  pinMode(MISO, OUTPUT);
  //SPCR &= ~(1<<MSTR);
  LightMax = analogRead(5)-0x20;

  SPDR = 0x1F;

  SPCR = (1<<SPE) | (1<<SPIE); 
}
ISR (SPI_STC_vect)
{

  SPI_DATA[SPI_DATA_POS] = SPDR;
  SPDR = 0x2F;
  SPI_DATA_POS++;
  if(SPI_DATA_POS >= 2){
    
    SPI_DATA_POS = 0;
    if((SPI_DATA[0]>>6) == 1)return;
    Mode = SPI_DATA[0]>>6;
    if(Mode == 2){
      Beat = SPI_DATA[1]&0x07;
      Bar = (SPI_DATA[1]>>4) |(( SPI_DATA[0]&0x3F)<<4);
    }
    else if(Mode == 3){
      Tempo = SPI_DATA[1] | ((SPI_DATA[0]&0x30)<<8);
    }

  }else if((SPI_DATA[0]>>6) == 1){
    SPDR = Light;
  }
}
const int digits[] = {
  0b00111111, // 0
  0b00000110, // 1
  0b01011011, // 2
  0b01001111, // 3
  0b01100110, // 4
  0b01101101, // 5
  0b01111101, // 6
  0b00100111, // 7
  0b01111111, // 8
  0b01101111, // 9
  0b01110111, // A
  0b01111100, // B
  0b00111001, // C
  0b01011110, // D
  0b01111001, // E
  0b01110001, // F
  0b00000000, // NC
};
void Write(int port, byte data){
  digitalWrite(port, LOW);
  for(int i =0; i< 8; i++){
    digitalWrite(i, (data>>i&1)); 
    delay(0.5);

    digitalWrite(i, LOW); 
  }

  digitalWrite(port, HIGH);
}

void Write(byte d1, byte d2, byte d3, byte d4){
  Write(NUM1, d1);
  Write(NUM2, d2);
  Write(NUM3, d3);
  Write(NUM4, d4);
}

void WriteNum(int num){
  byte d4 = digits[num%10];
  num /= 10;
  byte d3 = digits[num%10];
  num /= 10;
  byte d2 = digits[num%10];
  num /= 10;
  byte d1 = digits[num%10];
  Write(d1, d2, d3, d4);
}

void WriteBarBeat(int bar, byte beat){
  byte d3 = digits[bar%10] | 0x80;
  bar /= 10;
  byte d2 = digits[bar%10];
  bar /= 10;
  byte d1 = digits[bar%10];
  Write(d1, d2, d3, digits[beat]);
}

void WriteTempo(int tempo){
  byte d3 = digits[tempo%10];
  tempo /= 10;
  byte d2 = digits[tempo%10];
  tempo /= 10;
  byte d1 = digits[tempo%10];
  Write(0, d1, d2, d3);
}
int i = 0;
void loop(){
  if(Mode == 0) Write(0x80,0x80,0x80,0x80);
  else if(Mode == 2)WriteBarBeat(Bar, Beat+1);
  else if(Mode == 3)WriteTempo(Tempo);
  i++;
  if(i%10 == 0){
    Light = 255;
    int v = analogRead(5);
    if(v < 0x70)Light = 0;
    else if(v < LightMax) Light =(int)( ((double)(v-0x70) / LightMax) * 254);
    }
}













