import { baseRequestClient } from '#/api/request';

export function useWordApi() {
  return {
    GetData: () => {
      return baseRequestClient.get('/HotelRoom/Word/GetData');
    },
    Update: (data: object[]) => {
      return baseRequestClient.post('/HotelRoom/Word/Update', data);
    },
  };
}
