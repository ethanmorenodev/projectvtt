/**
 * Sesión del usuario: registro, login y logout contra el backend. El token
 * se guarda en localStorage y se inyecta en el cliente HTTP y en el hub.
 */

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from 'react';
import { apiFetch, setApiToken } from '../api/client';
import type { AuthResponse, UserDto } from '../api/types';

const TOKEN_KEY = 'projectvtt.token';

interface AuthValue {
  user: UserDto | null;
  loading: boolean;
  login: (userName: string, password: string) => Promise<void>;
  register: (userName: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthValue | null>(null);

function readStoredToken(): string | null {
  try {
    return localStorage.getItem(TOKEN_KEY);
  } catch {
    return null;
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserDto | null>(null);
  const [loading, setLoading] = useState(() => readStoredToken() !== null);

  useEffect(() => {
    const stored = readStoredToken();
    if (!stored) {
      return;
    }

    setApiToken(stored);
    let cancelled = false;

    void (async () => {
      try {
        const me = await apiFetch<UserDto>('/api/v1/auth/me');
        if (!cancelled) {
          setUser(me);
        }
      } catch {
        // Token caducado o revocado: se limpia la sesión local.
        setApiToken(null);
        localStorage.removeItem(TOKEN_KEY);
        if (!cancelled) {
          setUser(null);
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, []);

  const startSession = useCallback((response: AuthResponse) => {
    localStorage.setItem(TOKEN_KEY, response.token);
    setApiToken(response.token);
    setUser(response.user);
  }, []);

  const login = useCallback(
    async (userName: string, password: string) => {
      const response = await apiFetch<AuthResponse>('/api/v1/auth/login', {
        method: 'POST',
        body: { userName, password },
      });
      startSession(response);
    },
    [startSession],
  );

  const register = useCallback(
    async (userName: string, password: string) => {
      const response = await apiFetch<AuthResponse>('/api/v1/auth/register', {
        method: 'POST',
        body: { userName, password },
      });
      startSession(response);
    },
    [startSession],
  );

  const logout = useCallback(async () => {
    try {
      await apiFetch<void>('/api/v1/auth/logout', { method: 'POST' });
    } catch {
      // Si la sesión ya no es válida, la limpieza local es suficiente.
    }
    setApiToken(null);
    localStorage.removeItem(TOKEN_KEY);
    setUser(null);
  }, []);

  return (
    <AuthContext.Provider value={{ user, loading, login, register, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthValue {
  const value = useContext(AuthContext);
  if (!value) {
    throw new Error('useAuth debe usarse dentro de AuthProvider.');
  }
  return value;
}
