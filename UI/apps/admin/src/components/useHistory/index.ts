import { ref } from 'vue';

export function useHistory(key = 'mthotelcode_history', max = 20) {
  const history = ref(getHistory());

  function getHistory() {
    const data = localStorage.getItem(key);
    return data ? JSON.parse(data) : [];
  }

  function saveHistory(val: string) {
    if (!val) return;
    let arr = getHistory().filter((v) => v !== val);
    arr.unshift(val);
    if (arr.length > max) arr = arr.slice(0, max);
    localStorage.setItem(key, JSON.stringify(arr));
    history.value = arr;
  }

  function removeHistory(val: string) {
    const arr = getHistory().filter((v) => v !== val);
    localStorage.setItem(key, JSON.stringify(arr));
    history.value = arr;
  }

  function clearHistory() {
    localStorage.removeItem(key);
    history.value = [];
  }

  return { history, saveHistory, removeHistory, clearHistory };
}
