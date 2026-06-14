import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';

type QueryValue = string | number | boolean | null | undefined | readonly string[];

@Injectable({ providedIn: 'root' })
export class ApiClient {
  private readonly http = inject(HttpClient);

  get<T>(url: string, query?: Record<string, QueryValue>) {
    return this.http.get<T>(url, { params: this.toParams(query) });
  }

  post<T>(url: string, body?: unknown, query?: Record<string, QueryValue>) {
    return this.http.post<T>(url, body ?? {}, { params: this.toParams(query) });
  }

  put<T>(url: string, body: unknown) {
    return this.http.put<T>(url, body);
  }

  patch<T>(url: string, body: unknown) {
    return this.http.patch<T>(url, body);
  }

  delete<T>(url: string) {
    return this.http.delete<T>(url);
  }

  private toParams(query?: Record<string, QueryValue>) {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query ?? {})) {
      if (value === null || value === undefined || value === '') {
        continue;
      }

      if (Array.isArray(value)) {
        for (const item of value) {
          params = params.append(key, item);
        }
        continue;
      }

      params = params.set(key, String(value));
    }

    return params;
  }
}
