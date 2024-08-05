#!/bin/bash

BOT_PATH="./bomba-finder.py"
PID_FILE="./bot.pid"

start() {
    if [ -f $PID_FILE ]; then
        echo "Bomba Bot is already running."
        exit 1
    fi
    nohup python3 $BOT_PATH > bot.log 2>&1 &
    echo $! > $PID_FILE
    echo "Bomba Bot started."
}

stop() {
    if [ ! -f $PID_FILE ]; then
        echo "Bomba Bot is not running."
        exit 1
    fi
    kill $(cat $PID_FILE)
    rm $PID_FILE
    echo "Bomba Bot stopped."
}

case "$1" in
    start)
        start
        ;;
    stop)
        stop
        ;;
    *)
        echo "Usage: bomba {start|stop}"
        exit 1
        ;;
esac