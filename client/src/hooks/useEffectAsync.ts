import { useEffect, type DependencyList } from "react";

export const useEffectAsync = (effect: () => Promise<void>, deps?: DependencyList): void => {
    deps ||= [];
    
    useEffect(() => {
        (async () => {
            await effect();
        })();
    }, deps); // eslint-disable-line react-hooks/exhaustive-deps
}