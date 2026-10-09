import { ElMessage } from 'element-plus';

export function reportAccessError(error: unknown) {
  const response = (error as { response?: { data?: { detail?: string } } })
    ?.response;
  ElMessage.error(response?.data?.detail || '操作失败，请稍后重试');
}
