import { baseRequestClient } from '#/api/request';

export interface WeeklyAgentRawDto {
  statDate: string;
  supId: number;
  batchId: string;
  insertTime: string;
  totalCount: number;
  withMTHotelCodeCount: number;
  withRoomMapCount: number;
  unmatchedRecentCount: number | null;
}

export function useRoomMatchApi() {
  return {
    GetHotelRoomMapping: (params?: object) => {
      return baseRequestClient.get('/HotelRoom/OTAHotelRoom/GetHotelRoomMappingV2', {
        params,
      });
    },
    GetHotelRoomMappingCache: (params?: object) => {
      return baseRequestClient.get('/HotelRoom/OTAHotelRoom/GetHotelRoomMappingCache', {
        params,
      });
    },
    GetWeeklyAgentStats: (): Promise<WeeklyAgentRawDto[]> => {
      return baseRequestClient.get('/HotelRoom/OTAHotelRoom/GetWeeklyAgentStats');
    },
  };
}
