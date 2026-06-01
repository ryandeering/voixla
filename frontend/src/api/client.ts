import axios from "axios";

// BASE_URL is the Vite `base` (e.g. "/" or "/voixla/") so API URLs resolve under any sub-path.
export const apiClient = axios.create({
    baseURL: `${import.meta.env.BASE_URL}api`,
    headers: { "Content-Type": "application/json" },
});
